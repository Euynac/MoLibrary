using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monica.Guide;

/// <summary>Single composition API for a host program's <c>guide</c> dispatch.</summary>
public static class GuideCommandRunner
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) }
    };

    /// <summary>Parses, executes, renders, and returns the stable guide exit code.</summary>
    public static async Task<int> RunAsync(
        AgentProductDefinition definition,
        string[] guideArgs,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default,
        GuidePaths? enginePaths = null,
        AgentProductPaths? productPaths = null,
        Func<string>? currentVersion = null,
        string? currentAssemblyVersion = null,
        Func<GuideCommand, object, Task<object>>? transformReport = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(guideArgs);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            var command = GuideCommandParser.Parse(guideArgs);
            using var service = new AgentGuideService(
                definition,
                enginePaths,
                productPaths,
                currentVersion: currentVersion,
                currentAssemblyVersion: currentAssemblyVersion);
            // Phase lines stream while the command runs; JSON output keeps its document clean
            // by routing them to the error writer instead of stdout.
            IProgress<GuidePhase> progress = new PhaseReporter(command.Json ? error : output);
            var resolvedEnginePaths = enginePaths ?? GuidePaths.ForCurrentUser();
            // Source bindings and the issue preference are the machine-global agent policy;
            // only the owner product's guide exposes them, every other product stays on its
            // own installation surface.
            if (command.Name is "source" or "issue" && !definition.OwnsGlobalAgentPolicy)
            {
                throw new GuideUsageException(
                    $"{definition.DisplayName} does not own the machine-global agent policy; manage source bindings and issue reporting with the Monica guide.");
            }

            object report = command.Name switch
            {
                "overview" => await service.GetOverviewAsync(cancellationToken),
                "status" => await service.GetStatusAsync(
                    new GuideInspectRequest(command.Environments), progress, cancellationToken),
                "doctor" => await service.DiagnoseAsync(
                    new GuideInspectRequest(command.Environments), null, progress, cancellationToken),
                "configure" => await ConfigureAsync(definition, service, command, progress, cancellationToken),
                "unconfigure" => await UnconfigureAsync(service, command, progress, cancellationToken),
                "init" => await InitAsync(definition, resolvedEnginePaths, command, progress, cancellationToken),
                "forget" => await ForgetWorkspaceAsync(definition, resolvedEnginePaths, command, progress, cancellationToken),
                "workspaces" => ListWorkspaces(definition, resolvedEnginePaths),
                "source" => await SourceAsync(definition, resolvedEnginePaths, command, cancellationToken),
                "issue" => IssueAsync(resolvedEnginePaths, command),
                _ => throw new GuideUsageException($"Unknown command '{command.Name}'.")
            };
            // Engine-level extensions (global source bindings, the retired Node-era state, and
            // an optional workspace inspection) join the report before product extensions, so
            // every surface stays one envelope.
            report = AppendEngineExtensions(report, definition, command, resolvedEnginePaths);
            if (transformReport is not null)
            {
                report = await transformReport(command, report);
            }
            await RenderAsync(report, command, definition, currentVersion, output);
            return ExitCode(report);
        }
        catch (GuideUsageException exception)
        {
            await error.WriteLineAsync(exception.Message);
            await error.WriteLineAsync(Usage(definition));
            return 2;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync($"{definition.DisplayName} guide was cancelled.");
            return 2;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 2;
        }
    }

    private static Task<GuideReport> ConfigureAsync(
        AgentProductDefinition definition,
        AgentGuideService service,
        GuideCommand command,
        IProgress<GuidePhase> progress,
        CancellationToken cancellationToken)
    {
        var port = command.Port
                   ?? (definition.ServesLoopback
                       ? GuideProductConfiguration.Load(definition).Port
                       : null);
        // A workspace request installs its configured profile closure into the workspace's
        // project directories; selections stay the global-target flow (empty selections
        // refresh every recorded installation from this bundle).
        var selections = command.WorkspacePath is not null
            ? []
            : command.Environments
                .Select(environment => new GuideTargetSelection(
                    environment,
                    command.Targets.Count > 0 ? command.Targets : [GuideTarget.Shared]))
                .ToArray();
        var request = new GuideConfigureRequest(
            command.ExecutablePath,
            port is null ? null : new Uri($"http://localhost:{port}"),
            selections,
            command.ReleaseManifestPath,
            null,
            null,
            command.Profile,
            command.Skills.Count > 0 ? command.Skills : null,
            command.WorkspacePath,
            command.RuleSwitches);
        return command.Apply
            ? service.ApplyConfigureAsync(request, command.PlanDigest!, progress, cancellationToken)
            : service.PreviewConfigureAsync(request, progress, cancellationToken);
    }

    private static Task<GuideReport> UnconfigureAsync(
        AgentGuideService service,
        GuideCommand command,
        IProgress<GuidePhase> progress,
        CancellationToken cancellationToken)
    {
        var request = new GuideUnconfigureRequest(
            command.Targets.Count > 0 ? command.Targets : null,
            command.Environments.Count > 0 ? command.Environments : null,
            command.WorkspacePath);
        return command.Apply
            ? service.ApplyUnconfigureAsync(request, command.PlanDigest!, progress, cancellationToken)
            : service.PreviewUnconfigureAsync(request, progress, cancellationToken);
    }

    /// <summary>Lists every registered workspace with its live health as one stable envelope.</summary>
    private static GuideReport ListWorkspaces(AgentProductDefinition definition, GuidePaths enginePaths)
    {
        var catalog = GuideWorkspaceService.TryLoadProductCatalog(definition, enginePaths);
        var service = new GuideWorkspaceService(definition, enginePaths, catalog);
        var views = service.ListWorkspaces();
        var checks = views
            .Select(view =>
            {
                var healthy = view.DirectoryExists && view.ConfigurationCurrent && view.Issues.Count == 0;
                var status = !view.DirectoryExists
                    ? GuideCheckStatus.Warning
                    : healthy ? GuideCheckStatus.Ok : GuideCheckStatus.Warning;
                var details = new Dictionary<string, string>
                {
                    ["profile"] = view.Profile,
                    ["skills"] = $"{view.InstalledSkillCount}/{view.ProfileSkillCount}",
                    ["instructions"] = view.InstructionsCurrent ? "current" : "stale"
                };
                return new GuideCheck(
                    $"workspace.{AgentGuideService.TargetCheckName(GuideTarget.Project, view.Workspace)}",
                    status,
                    view.Issues.Count == 0
                        ? $"{view.Workspace}: {view.Profile}, {view.InstalledSkillCount}/{view.ProfileSkillCount} profile skills installed."
                        : $"{view.Workspace}: {string.Join(" ", view.Issues)}",
                    view.DirectoryExists ? null : "Restore the workspace directory or forget it with: guide forget --workspace <path>.",
                    details);
            })
            .ToArray();
        var reportStatus = checks.Any(static check => check.Status == GuideCheckStatus.Error)
            ? GuideStatus.Error
            : checks.Any(static check => check.Status == GuideCheckStatus.Warning)
                ? GuideStatus.Warning
                : GuideStatus.Ready;
        return new GuideReport(
            GuideContractVersions.CURRENT,
            AgentGuideInfo.CurrentVersion(),
            reportStatus,
            checks,
            new GuideSummary(
                "workspaces",
                checks.Length == 0
                    ? "No workspaces are registered."
                    : $"{checks.Length} registered workspace(s).",
                DateTimeOffset.UtcNow,
                ["Initialize one with: guide init --workspace <path> --profile <profile>."]));
    }

    private static Task<GuideReport> InitAsync(
        AgentProductDefinition definition,
        GuidePaths enginePaths,
        GuideCommand command,
        IProgress<GuidePhase> progress,
        CancellationToken cancellationToken)
    {
        var workspace = ResolveWorkspaceCatalog(definition, enginePaths);
        var service = new GuideWorkspaceService(definition, enginePaths, workspace);
        var request = new GuideWorkspaceInitRequest(
            command.WorkspacePath!,
            command.Profile,
            command.Capabilities,
            command.RuleSwitches);
        return command.Apply
            ? service.InitAsync(request, command.PlanDigest!, progress, cancellationToken)
            : service.InitAsync(request, null, progress, cancellationToken);
    }

    private static Task<GuideReport> ForgetWorkspaceAsync(
        AgentProductDefinition definition,
        GuidePaths enginePaths,
        GuideCommand command,
        IProgress<GuidePhase> progress,
        CancellationToken cancellationToken)
    {
        var workspace = ResolveWorkspaceCatalog(definition, enginePaths);
        var service = new GuideWorkspaceService(definition, enginePaths, workspace);
        return command.Apply
            ? service.ForgetAsync(command.WorkspacePath!, command.PlanDigest!, progress, cancellationToken)
            : service.ForgetAsync(command.WorkspacePath!, null, progress, cancellationToken);
    }

    private static Task<GuideReport> SourceAsync(
        AgentProductDefinition definition,
        GuidePaths enginePaths,
        GuideCommand command,
        CancellationToken cancellationToken)
    {
        var catalog = GuideWorkspaceService.TryLoadProductCatalog(definition, enginePaths);
        var service = new GuideSourceService(enginePaths, catalog);
        return command.SourceAction switch
        {
            "list" => Task.FromResult(service.List()),
            "resolve" => Task.FromResult(service.Resolve(command.Repository!)),
            "bind" => service.BindAsync(
                new GuideSourceBindRequest(command.Repository!, command.SourcePath, command.SourceRef, command.ResolverPath),
                command.Apply ? command.PlanDigest : null,
                null,
                cancellationToken),
            "unbind" => service.UnbindAsync(
                command.Repository!,
                command.Apply ? command.PlanDigest : null,
                null,
                cancellationToken),
            _ => throw new GuideUsageException($"Unknown source action '{command.SourceAction}'.")
        };
    }

    private static SkillCatalog ResolveWorkspaceCatalog(AgentProductDefinition definition, GuidePaths enginePaths)
        => GuideWorkspaceService.TryLoadProductCatalog(definition, enginePaths)
           ?? throw new GuideUsageException(
               $"{definition.DisplayName} is not installed or its bundle defines no workspace profiles; install it with guide configure before initializing a workspace.");

    /// <summary>
    /// Reads or sets the machine-global issue-reporting mode. The write is an immediate
    /// preference save — like the wizard's preference toggle it is not governed state and
    /// takes no plan digest; the mode bounds preparation only and never authorizes a remote
    /// action.
    /// </summary>
    private static GuideReport IssueAsync(GuidePaths enginePaths, GuideCommand command)
    {
        if (command.IssueAction == "set")
        {
            GuideIssuePreferencesStore.Save(
                enginePaths,
                GuideIssuePreferencesStore.Load(enginePaths) with
                {
                    IssueReporting = Enum.Parse<GuideIssueReportingMode>(command.IssueMode!, ignoreCase: true)
                });
        }

        var mode = GuideIssuePreferencesStore.Load(enginePaths).IssueReporting;
        var modeName = mode.ToString().ToLowerInvariant();
        var check = new GuideCheck(
            "issue.reporting",
            GuideCheckStatus.Ok,
            $"Issue reporting mode: {modeName}. {mode.Describe()}",
            null,
            new Dictionary<string, string> { ["mode"] = modeName });
        return new GuideReport(
            GuideContractVersions.CURRENT,
            AgentGuideInfo.CurrentVersion(),
            GuideStatus.Ready,
            [check],
            new GuideSummary(
                command.IssueAction!,
                command.IssueAction == "set"
                    ? $"Issue reporting mode set to {modeName}."
                    : $"Issue reporting mode is {modeName}.",
                DateTimeOffset.UtcNow,
                ["The mode bounds preparation only; a persisted preference never authorizes a remote action."]),
            null);
    }

    /// <summary>
    /// Appends engine-level checks to read-only reports: the machine-global agent policy
    /// (source binding health and the retired Node-era guide state) joins only the owning
    /// product's reports, while the workspace instruction inspection follows --workspace for
    /// any product with a workspace catalog.
    /// </summary>
    private static object AppendEngineExtensions(
        object report,
        AgentProductDefinition definition,
        GuideCommand command,
        GuidePaths enginePaths)
    {
        if (command.Name is not ("overview" or "status" or "doctor"))
        {
            return report;
        }

        var catalog = GuideWorkspaceService.TryLoadProductCatalog(definition, enginePaths);
        var checks = new List<GuideCheck>();
        if (catalog is not null && definition.OwnsGlobalAgentPolicy)
        {
            // Bindings are machine-global; only the policy owner's read-only surfaces
            // observe them, and the service stays silent while nothing is bound.
            checks.AddRange(new GuideSourceService(enginePaths, catalog).HealthChecks());
            var legacy = LegacyNodeStateCheck();
            if (legacy is not null)
            {
                checks.Add(legacy);
            }
        }

        if (command.WorkspacePath is not null && catalog is not null)
        {
            var workspaceService = new GuideWorkspaceService(definition, enginePaths, catalog);
            checks.AddRange(workspaceService.Inspect(command.WorkspacePath).Checks);
        }

        return AppendChecks(report, checks);
    }

    /// <summary>Warns when the retired Node-based Monica Guide user state still exists.</summary>
    private static GuideCheck? LegacyNodeStateCheck()
    {
        var environment = new GuideHostEnvironment();
        var candidates = new[]
        {
            Path.Combine(environment.LocalApplicationDataDirectory, "Monica Guide", "state.json"),
            Path.Combine(environment.UserHomeDirectory, ".local", "share", "monica-guide", "state.json"),
            Path.Combine(environment.UserHomeDirectory, "Library", "Application Support", "Monica Guide", "state.json")
        };
        var existing = candidates.FirstOrDefault(File.Exists);
        return existing is null
            ? null
            : new GuideCheck(
                "legacy-guide-state",
                GuideCheckStatus.Warning,
                $"A retired Node-based Monica Guide user state exists at {existing}.",
                "Re-record needed source bindings with guide source bind, then delete the legacy file after review.");
    }

    /// <summary>
    /// Appends product-extension checks (for example a product-specific workspace inspection)
    /// to an engine report and re-aggregates its status.
    /// </summary>
    public static object AppendChecks(object report, IReadOnlyList<GuideCheck> additional)
    {
        if (additional.Count == 0)
        {
            return report;
        }

        return report switch
        {
            GuideReport guide => guide with
            {
                Checks = [.. guide.Checks, .. additional],
                Status = Aggregate(guide.Status, additional)
            },
            GuideHealthReport health => health with
            {
                Checks = [.. health.Checks, .. additional],
                Status = Aggregate(health.Status, additional)
            },
            _ => report
        };
    }

    private static GuideStatus Aggregate(GuideStatus current, IReadOnlyList<GuideCheck> additional)
        => additional.Any(static check => check.Status == GuideCheckStatus.Error)
            ? GuideStatus.Error
            : additional.Any(static check => check.Status == GuideCheckStatus.Warning) && current == GuideStatus.Ready
                ? GuideStatus.Warning
                : current;

    /// <summary>Streams guide phases as plain terminal lines while a command runs.</summary>
    private sealed class PhaseReporter(TextWriter writer) : IProgress<GuidePhase>
    {
        public void Report(GuidePhase value)
        {
            if (value.Total > 0)
            {
                writer.WriteLine($"[{value.Completed}/{value.Total}] {value.Message}");
                return;
            }
            writer.WriteLine(value.Message);
        }
    }

    private static async Task RenderAsync(
        object report,
        GuideCommand command,
        AgentProductDefinition definition,
        Func<string>? currentVersion,
        TextWriter output)
    {
        if (command.Json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(report, report.GetType(), JSON_OPTIONS));
            return;
        }

        switch (report)
        {
            case GuideReport guide:
                await RenderHumanAsync(definition, currentVersion, guide.Status, guide.Summary, guide.Checks, guide.Plan, command.Locale, output);
                break;
            case GuideHealthReport health:
                await RenderHumanAsync(definition, currentVersion, health.Status, health.Summary, health.Checks, health.Plan, command.Locale, output);
                break;
        }
    }

    private static async Task RenderHumanAsync(
        AgentProductDefinition definition,
        Func<string>? currentVersion,
        GuideStatus status,
        GuideSummary summary,
        IReadOnlyList<GuideCheck> checks,
        GuidePlan? plan,
        string? locale,
        TextWriter output)
    {
        var version = currentVersion?.Invoke() ?? AgentGuideInfo.CurrentVersion();
        if (summary.Command == "overview")
        {
            await output.WriteLineAsync($"{definition.DisplayName} {version}");
        }
        else
        {
            await output.WriteLineAsync($"{definition.DisplayName} {version} — {status.ToString().ToLowerInvariant()}\n{summary.Message}");
        }
        foreach (var check in checks)
        {
            await output.WriteLineAsync($"[{check.Status.ToString().ToLowerInvariant()}] {check.Id}: {check.Message}");
            if (!string.IsNullOrWhiteSpace(check.Remediation)) await output.WriteLineAsync($"  {check.Remediation}");
        }
        if (plan is not null)
        {
            await output.WriteLineAsync($"Plan digest: {plan.PlanDigest}");
            foreach (var action in plan.Actions) await output.WriteLineAsync($"  {action.Kind}: {action.Target}");
        }
        foreach (var action in summary.NextActions) await output.WriteLineAsync($"Next: {action}");
    }

    private static int ExitCode(object report)
        => report switch
        {
            GuideReport { Status: GuideStatus.Ready } => 0,
            GuideHealthReport { Status: GuideStatus.Ready } => 0,
            GuideReport { Status: GuideStatus.Warning } => 1,
            GuideHealthReport { Status: GuideStatus.Warning } => 1,
            GuideReport { Status: GuideStatus.Error } => 3,
            GuideHealthReport { Status: GuideStatus.Error } => 3,
            _ => 2
        };

    /// <summary>
    /// Usage lines for one product; the source and issue lines exist only for the owner of
    /// the machine-global agent policy.
    /// </summary>
    private static string Usage(AgentProductDefinition definition)
    {
        var core =
            "Usage: guide <overview|status|doctor|configure|unconfigure> [--json] " +
            "[--locale en-US|zh-CN] " +
            "[--target shared|claude]... [--environment windows|wsl:<distro>[:<user>]]... " +
            "[--skill <name>]... [--profile <name>] [--executable <path>] [--release-manifest <path>] " +
            "[--port <1-65535>] [--apply --plan-digest <sha256>]\n" +
            "       guide configure --workspace <path> [--profile <name>] [--apply --plan-digest <sha256>]\n" +
            "       guide unconfigure --workspace <path> [--apply --plan-digest <sha256>]\n" +
            "       guide init --workspace <path> --profile <name> [--capability <name>]... " +
            "[--apply --plan-digest <sha256>]\n" +
            "       guide forget --workspace <path> [--apply --plan-digest <sha256>]\n" +
            "       guide workspaces [--json]";
        return definition.OwnsGlobalAgentPolicy
            ? core
              + "\n       guide source <list|resolve|bind|unbind> [--repository monica|docs] " +
              "[--source-path <path>] [--source-ref <ref>] [--source-resolver <path>] " +
              "[--apply --plan-digest <sha256>]\n" +
              "       guide issue <status|set> [--mode prepare|ask|never]"
            : core;
    }
}
