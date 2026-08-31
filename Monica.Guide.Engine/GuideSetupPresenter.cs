using System.Text.Json;

namespace Monica.Guide;

/// <summary>One human-facing entry of a previewed guide plan.</summary>
public sealed record SetupPlanEntry(string Target, string Description);

/// <summary>Actions of one kind, ordered for display; NoChange entries collapse into a count.</summary>
public sealed record SetupPlanGroup(GuidePlanActionKind Kind, int Count, IReadOnlyList<SetupPlanEntry> Entries);

/// <summary>Culture-neutral projection of a guide plan for setup surfaces.</summary>
public sealed record SetupPlanView(
    bool IsNoOp,
    int ChangeCount,
    IReadOnlyList<SetupPlanGroup> ChangeGroups);

/// <summary>One human-facing guide check.</summary>
public sealed record SetupCheckView(
    string Id,
    GuideCheckStatus Status,
    string Message,
    string? Remediation);

/// <summary>Culture-neutral projection of guide checks with failures and blockers first.</summary>
public sealed record SetupChecksView(
    GuideStatus Status,
    IReadOnlyList<SetupCheckView> Items)
{
    public bool HasErrors => Status == GuideStatus.Error;

    public IReadOnlyList<SetupCheckView> OrderedItems => Items
        .OrderBy(static item => item.Status switch
        {
            GuideCheckStatus.Error => 0,
            GuideCheckStatus.Warning => 1,
            _ => 2,
        })
        .ThenBy(static item => item.Id, StringComparer.Ordinal)
        .ToArray();
}

/// <summary>Validated bundle candidate for installation.</summary>
public sealed record SetupBundleView(
    bool Exists,
    bool HasManifest,
    string ApplicationDirectory,
    string? ProductId,
    string? ProductName,
    string? ProductVersion,
    string? Tag,
    string? ManifestDigest,
    string? Error);

/// <summary>One agent host observed in one environment by a guide status report.</summary>
public sealed record SetupAgentPresence(
    GuideAgent Agent,
    string EnvironmentSelector,
    string? Version);

/// <summary>Culture-neutral presentation of guide plans, checks, bundles, and detected hosts.</summary>
public static class GuideSetupPresenter
{
    private static readonly GuidePlanActionKind[] GroupOrderArray =
    [
        GuidePlanActionKind.ReplaceSkillDirectory,
        GuidePlanActionKind.DeleteSkillDirectory,
        GuidePlanActionKind.WriteFile,
        GuidePlanActionKind.DeleteFile
    ];

    /// <summary>Groups plan actions by kind for a compact directory-level preview.</summary>
    public static SetupPlanView PresentPlan(GuidePlan? plan)
    {
        if (plan is null)
        {
            return new SetupPlanView(false, 0, []);
        }

        var changes = plan.Actions.ToArray();
        var groups = changes
            .GroupBy(static action => action.Kind)
            .Select(static group => new SetupPlanGroup(
                group.Key,
                group.Count(),
                group
                    .Select(static action => new SetupPlanEntry(action.Target, action.Description))
                    .ToArray()))
            .OrderBy(static group => Array.IndexOf(GroupOrderArray, group.Kind) is var index and >= 0 ? index : GroupOrderArray.Length)
            .ThenBy(static group => group.Kind)
            .ToArray();
        return new SetupPlanView(plan.IsNoOp, changes.Length, groups);
    }

    /// <summary>Projects guide checks, surfacing the aggregate status and every detail.</summary>
    public static SetupChecksView PresentChecks(IEnumerable<GuideCheck>? checks, GuideStatus status)
        => new(
            status,
            (checks ?? [])
            .Select(static check => new SetupCheckView(check.Id, check.Status, check.Message, check.Remediation))
            .ToArray());

    /// <summary>Validates one bundle candidate directory and projects its release identity.</summary>
    public static SetupBundleView PresentBundle(string? candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return new SetupBundleView(false, false, string.Empty, null, null, null, null, null, null);
        }

        try
        {
            // A candidate bundle may carry any product version; it is validated as a
            // self-consistent release rather than against any one process's build.
            var manifestDirectory = Path.Combine(candidatePath.Trim(), "app");
            if (!Directory.Exists(manifestDirectory))
            {
                manifestDirectory = candidatePath.Trim();
            }
            var release = GuideReleaseMetadata.Observe(manifestDirectory, null);
            var manifest = release.Manifest;
            if (manifest is null)
            {
                return new SetupBundleView(
                    false,
                    false,
                    manifestDirectory,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "The selected directory carries no release manifest; choose an extracted release bundle root.");
            }
            return new SetupBundleView(
                true,
                true,
                manifestDirectory,
                manifest.ProductId,
                manifest.ProductName,
                manifest.ProductVersion,
                manifest.Tag,
                release.ManifestDigest,
                null);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or IOException)
        {
            return new SetupBundleView(
                true,
                true,
                candidatePath.Trim(),
                null,
                null,
                null,
                null,
                null,
                exception.Message);
        }
    }

    /// <summary>Validated release bundles extracted beside one bundle root, oldest-name first.</summary>
    public static IReadOnlyList<SetupBundleView> PresentSiblingBundles(string bundleRoot, string directoryPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPrefix);
        var parent = Directory.GetParent(Path.GetFullPath(bundleRoot));
        if (parent is null)
        {
            return [];
        }

        return parent.EnumerateDirectories(directoryPrefix + "*")
            .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
            .Select(directory => PresentBundle(directory.FullName))
            .Where(static view => view.Exists && view.Error is null)
            .ToArray();
    }

    /// <summary>Derives the agent hosts observed by a status report from its stable check ids.</summary>
    public static IReadOnlyList<SetupAgentPresence> DeriveAgentPresence(GuideReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var presence = new HashSet<SetupAgentPresence>();
        foreach (var check in report.Checks)
        {
            var segments = check.Id.Split('.');
            if (segments.Length < 4
                || segments[^1] != "detected"
                || segments[0] != "agent"
                || !Enum.TryParse<GuideAgent>(segments[1], ignoreCase: true, out var agent))
            {
                continue;
            }

            var version = check.Details?.TryGetValue("version", out var observed) == true
                           && !string.IsNullOrWhiteSpace(observed)
                ? observed
                : null;
            presence.Add(new SetupAgentPresence(agent, segments[2], version));
        }

        return presence
            .OrderBy(static item => item.EnvironmentSelector, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Agent)
            .ToArray();
    }

    /// <summary>
    /// Resolves the default release bundle root by structure: the bundle that contains this
    /// running executable (<c>&lt;bundle&gt;/app/</c> or <c>&lt;bundle&gt;/setup/</c>) and carries
    /// the structural release markers. Full manifest validation still happens when the path is
    /// validated. Null when this executable does not run from a recognizable bundle.
    /// </summary>
    public static string? DetectBundleRootByStructure(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        var current = Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(Path.GetFileName(current), "setup", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var candidate = Directory.GetParent(current)?.FullName;
        if (candidate is null
            || !File.Exists(Path.Combine(candidate, "release-manifest.json"))
            || !File.Exists(Path.Combine(candidate, "skills", "catalog.json")))
        {
            return null;
        }
        return candidate;
    }
}
