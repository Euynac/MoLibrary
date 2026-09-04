using System.Text.Json;

namespace Monica.Guide;

/// <summary>
/// Workspace facts shared by the guide services: the repository-shared
/// <c>.monica/guide.json</c> configuration and the engine-level workspace registry at
/// <c>state/workspaces.json</c>. The registry is what listing surfaces (the wizard and
/// <c>guide workspaces</c>) enumerate; the per-workspace file stays the portable truth.
/// </summary>
internal static class GuideWorkspaceStore
{
    private const string CONFIG_DIRECTORY_NAME = ".monica";
    private const string CONFIG_FILE_NAME = "guide.json";

    /// <summary>
    /// Repository-shared workspace configuration. Shared facts only: the initializing
    /// product, profile, capabilities, Claude import, and skill targets. Per-product state
    /// such as the managed instruction version lives in the engine registry, keyed by
    /// workspace and product, so several products can guide one workspace.
    /// </summary>
    internal sealed record GuideWorkspaceConfig(
        int SchemaVersion,
        string ProductId,
        string Profile,
        IReadOnlyList<string> Capabilities,
        bool ManagedClaudeImport,
        IReadOnlyList<string>? SkillTargets = null)
    {
        public const int CurrentSchemaVersion = 1;

        /// <summary>Workspace-relative skill directories; the default mirrors the shared agent catalog convention.</summary>
        public IReadOnlyList<string> EffectiveSkillTargets()
            => SkillTargets is { Count: > 0 } targets ? targets : [".agents/skills"];
    }

    internal static string ConfigPath(string workspaceRoot)
        => Path.Combine(workspaceRoot, CONFIG_DIRECTORY_NAME, CONFIG_FILE_NAME);

    /// <summary>
    /// Loads the workspace configuration; a missing file returns null without an issue, a
    /// legacy or unreadable file returns an issue check describing the recovery.
    /// </summary>
    internal static GuideWorkspaceConfig? LoadConfig(string workspaceRoot, out string? issue)
    {
        issue = null;
        var path = ConfigPath(workspaceRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var schemaVersion = document.RootElement.TryGetProperty("schemaVersion", out var version)
                ? version.GetInt32()
                : 0;
            if (schemaVersion != GuideWorkspaceConfig.CurrentSchemaVersion)
            {
                issue = $"Workspace configuration schema {schemaVersion} predates this guide; initialize again to rewrite it.";
                return null;
            }

            return JsonSerializer.Deserialize<GuideWorkspaceConfig>(File.ReadAllBytes(path), GuidePlanning.JsonOptions)
                   ?? throw new JsonException("The workspace configuration is empty.");
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            issue = $"Workspace configuration is unreadable: {exception.Message}";
            return null;
        }
    }

    /// <summary>
    /// Resolves the configured skill target directories to absolute roots. Every entry must
    /// be workspace-relative and stay inside the workspace, so a workspace can never point
    /// the guide at arbitrary machine directories.
    /// </summary>
    internal static IReadOnlyList<string> ResolveSkillTargets(
        string workspaceRoot,
        GuideWorkspaceConfig config,
        ICollection<string>? issues = null)
    {
        var roots = new List<string>();
        foreach (var relative in config.EffectiveSkillTargets())
        {
            if (string.IsNullOrWhiteSpace(relative)
                || Path.IsPathRooted(relative)
                || relative.Split('\\', '/').Contains("..", StringComparer.Ordinal))
            {
                issues?.Add($"Skill target '{relative}' must be a workspace-relative directory inside the workspace.");
                continue;
            }

            var root = Path.GetFullPath(Path.Combine(workspaceRoot, relative.TrimEnd('\\', '/')));
            if (!root.StartsWith(Path.GetFullPath(workspaceRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                issues?.Add($"Skill target '{relative}' escapes the workspace.");
                continue;
            }

            roots.Add(root);
        }

        return roots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static root => root, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

/// <summary>The engine-level registry of initialized workspaces across products.</summary>
internal sealed record GuideWorkspaceRegistry(int SchemaVersion, IReadOnlyList<GuideWorkspaceEntry> Workspaces)
{
    public const int CurrentSchemaVersion = 1;
}

/// <summary>One registered workspace: a portable summary of its last approved initialization.</summary>
internal sealed record GuideWorkspaceEntry(
    string Workspace,
    string ProductId,
    string Profile,
    IReadOnlyList<string> Capabilities,
    int InstructionBlockVersion);

internal static class GuideWorkspaceRegistryFile
{
    internal static string PathFor(GuidePaths enginePaths)
        => System.IO.Path.Combine(enginePaths.StateDirectory, "workspaces.json");

    internal static GuideWorkspaceRegistry? Load(GuidePaths enginePaths)
    {
        var path = PathFor(enginePaths);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var registry = JsonSerializer.Deserialize<GuideWorkspaceRegistry>(
                File.ReadAllBytes(path), GuidePlanning.JsonOptions);
            return registry is not null && registry.SchemaVersion == GuideWorkspaceRegistry.CurrentSchemaVersion
                ? registry
                : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Returns the registry with one entry upserted; keyed by workspace path and product.</summary>
    internal static GuideWorkspaceRegistry Upsert(
        GuideWorkspaceRegistry? registry,
        GuideWorkspaceEntry entry)
    {
        var entries = (registry?.Workspaces ?? [])
            .Where(existing => !(string.Equals(existing.Workspace, entry.Workspace, StringComparison.OrdinalIgnoreCase)
                                 && string.Equals(existing.ProductId, entry.ProductId, StringComparison.Ordinal)))
            .ToList();
        entries.Add(entry);
        return Order(new GuideWorkspaceRegistry(
            GuideWorkspaceRegistry.CurrentSchemaVersion,
            entries));
    }

    /// <summary>Returns the registry with one workspace's entry (any product) removed.</summary>
    internal static GuideWorkspaceRegistry Remove(
        GuideWorkspaceRegistry? registry,
        string workspace,
        string productId)
    {
        var entries = (registry?.Workspaces ?? [])
            .Where(existing => !(string.Equals(existing.Workspace, workspace, StringComparison.OrdinalIgnoreCase)
                                 && string.Equals(existing.ProductId, productId, StringComparison.Ordinal)))
            .ToList();
        return Order(new GuideWorkspaceRegistry(GuideWorkspaceRegistry.CurrentSchemaVersion, entries));
    }

    private static GuideWorkspaceRegistry Order(GuideWorkspaceRegistry registry)
        => registry with
        {
            Workspaces = registry.Workspaces
                .OrderBy(static entry => entry.Workspace, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static entry => entry.ProductId, StringComparer.Ordinal)
                .ToArray()
        };
}
