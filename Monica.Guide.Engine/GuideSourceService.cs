using System.Diagnostics;
using System.Text.Json;

namespace Monica.Guide;

/// <summary>
/// One verified first-party source checkout bound globally as a lookup-only locator. The
/// binding records exact provenance; it never grants write permission.
/// </summary>
public sealed record GuideSourceBinding(
    string Repository,
    string Ref,
    string Commit,
    string SourcePath,
    string ResolutionKind,
    string Provenance);

/// <summary>Health of one stored binding observed at lookup time.</summary>
public sealed record GuideSourceObservation(
    string Repository,
    string StoredRef,
    string StoredCommit,
    string SourcePath,
    string? ObservedCommit,
    string PathHealth,
    bool? Dirty,
    string? Identity,
    IReadOnlyList<GuideCheck> Warnings);

/// <summary>Desired source binding change.</summary>
public sealed record GuideSourceBindRequest(
    string Repository,
    string? SourcePath,
    string? SourceRef,
    string? ResolverPath = null);

/// <summary>
/// Global source bindings for first-party repositories (Monica, Monica.Docs). Bindings live
/// in the guide engine data root beside the ownership ledger, are verified against a local
/// canonical Git checkout or the pinned <c>inspect-dependency-source</c> resolver, and every
/// mutation goes through the engine's preview-first, digest-locked plan flow.
/// </summary>
public sealed class GuideSourceService
{
    /// <summary>Fallback registry when the product bundle catalog declares no source repositories.</summary>
    private static readonly (string Repository, string[] Aliases)[] KNOWN_REPOSITORIES =
    [
        ("Tairitsua/Monica", ["monica"]),
        ("Tairitsua/Monica.Docs", ["docs"])
    ];

    private readonly GuidePaths _enginePaths;
    private readonly SkillCatalog? _catalog;
    private readonly IGuideGitProbe _git;
    private readonly IGuideSourceResolver? _resolver;

    public GuideSourceService(
        GuidePaths enginePaths,
        SkillCatalog? catalog = null,
        IGuideGitProbe? git = null,
        IGuideSourceResolver? resolver = null)
    {
        ArgumentNullException.ThrowIfNull(enginePaths);
        _enginePaths = enginePaths;
        _catalog = catalog;
        _git = git ?? new GuideGitProbe();
        _resolver = resolver;
    }

    private string LedgerFile => Path.Combine(_enginePaths.StateDirectory, "source-bindings.json");

    /// <summary>Health of every recorded binding, for doctor surfaces; unbound repositories stay silent.</summary>
    public IReadOnlyList<GuideCheck> HealthChecks()
    {
        var (ledger, ledgerIssue) = LoadLedger();
        if (ledgerIssue is not null)
        {
            return [ledgerIssue];
        }

        if (ledger is null || ledger.Bindings.Count == 0)
        {
            return [];
        }

        return ledger.Bindings.Values
            .OrderBy(static binding => binding.Repository, StringComparer.Ordinal)
            .Select(binding => BindingCheck(Observe(binding)))
            .ToArray();
    }

    /// <summary>Lists every declared repository with its binding health.</summary>
    public GuideReport List()
    {
        var (ledger, ledgerIssue) = LoadLedger();
        var checks = new List<GuideCheck>();
        if (ledgerIssue is not null)
        {
            checks.Add(ledgerIssue);
        }

        foreach (var repository in DeclaredRepositories())
        {
            var binding = ledger?.Bindings.GetValueOrDefault(repository.Repository);
            checks.Add(binding is null
                ? Check(SourceCheckId(repository.Repository), GuideCheckStatus.Ok,
                    $"{repository.Repository} is not bound.")
                : BindingCheck(Observe(binding)));
        }

        return Report("source list", checks,
            ledger is null || ledger.Bindings.Count == 0
                ? "No global source bindings are recorded."
                : $"{ledger.Bindings.Count} global source binding(s) recorded.",
            ["Bindings are global lookup locators; they never grant write permission."]);
    }

    /// <summary>Observes one stored binding without mutating anything.</summary>
    public GuideReport Resolve(string repositorySelector)
    {
        var (repository, resolveCheck) = ResolveRepository(repositorySelector);
        if (resolveCheck is not null)
        {
            return Report("source resolve", [resolveCheck], "Repository selector is invalid.", []);
        }

        var (ledger, ledgerIssue) = LoadLedger();
        var checks = new List<GuideCheck>();
        if (ledgerIssue is not null)
        {
            checks.Add(ledgerIssue);
        }

        var binding = ledger?.Bindings.GetValueOrDefault(repository);
        if (binding is null)
        {
            checks.Add(Check(SourceCheckId(repository), GuideCheckStatus.Warning,
                $"{repository} is not bound.", $"Bind it with: guide source bind --repository {repository}."));
            return Report("source resolve", checks, $"{repository} is unbound.",
                [$"Bind a verified checkout with: guide source bind --repository {repository} --source-path <path>."]);
        }

        var observation = Observe(binding);
        checks.Add(BindingCheck(observation));
        checks.AddRange(observation.Warnings);
        var next = observation.PathHealth == "available" && observation.Dirty != true
            ? new[] { $"Use {binding.SourcePath} as a verified lookup location for {repository}." }
            : new[] { "Treat the reported path with care; the observation above lists its health warnings." };
        return Report("source resolve", checks, $"{repository}: {observation.PathHealth}.", next);
    }

    /// <summary>Previews or applies binding one verified source checkout globally.</summary>
    public async Task<GuideReport> BindAsync(
        GuideSourceBindRequest request,
        string? expectedDigest = null,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var guideLock = expectedDigest is not null
            ? await GuideMutations.AcquireLockAsync(_enginePaths, cancellationToken)
            : null;
        var (prepared, blockers) = BuildBindPlan(request);
        return await CompleteAsync(prepared, blockers, expectedDigest, progress, cancellationToken);
    }

    /// <summary>Previews or applies removing one global source binding.</summary>
    public async Task<GuideReport> UnbindAsync(
        string repositorySelector,
        string? expectedDigest = null,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var guideLock = expectedDigest is not null
            ? await GuideMutations.AcquireLockAsync(_enginePaths, cancellationToken)
            : null;
        var (prepared, blockers) = BuildUnbindPlan(repositorySelector);
        return await CompleteAsync(prepared, blockers, expectedDigest, progress, cancellationToken);
    }

    private (GuidePreparedPlan Plan, IReadOnlyList<GuideCheck> Blockers) BuildBindPlan(GuideSourceBindRequest request)
    {
        var blockers = new List<GuideCheck>();
        var checks = new List<GuideCheck>();
        var mutations = new List<GuidePlannedMutation>();
        var (repository, selectorCheck) = ResolveRepository(request.Repository);
        if (selectorCheck is not null)
        {
            blockers.Add(selectorCheck);
            return (GuideMutations.Prepare("source bind", mutations, blockers), blockers);
        }

        var (ledger, ledgerIssue) = LoadLedger();
        if (ledgerIssue is not null)
        {
            blockers.Add(ledgerIssue);
        }

        GuideSourceBinding? binding = null;
        try
        {
            binding = request.SourcePath is not null
                ? VerifyLocalBinding(repository, request.SourcePath, request.SourceRef)
                : ResolveCachedBinding(repository, request.SourceRef, request.ResolverPath);
        }
        catch (GuideSourceException exception)
        {
            blockers.Add(Check(SourceCheckId(repository), GuideCheckStatus.Error, exception.Message, exception.Remediation));
        }

        if (binding is not null)
        {
            var observation = Observe(binding);
            checks.Add(BindingCheck(observation));
            checks.AddRange(observation.Warnings);
            var next = new Dictionary<string, GuideSourceBinding>(ledger?.Bindings ?? EmptyBindings(), StringComparer.Ordinal)
            {
                [repository] = binding
            };
            GuideMutations.AddLocalWrite(
                "source.bindings.write",
                LedgerFile,
                GuidePlanning.JsonBytes(new GuideSourceBindingLedger(GuideSourceBindingLedger.CurrentSchemaVersion, next)),
                $"Record the global {repository} source binding.",
                mutations);
        }

        var prepared = GuideMutations.Prepare("source bind", mutations, [.. checks, .. blockers]);
        return (prepared, blockers);
    }

    private (GuidePreparedPlan Plan, IReadOnlyList<GuideCheck> Blockers) BuildUnbindPlan(string repositorySelector)
    {
        var blockers = new List<GuideCheck>();
        var checks = new List<GuideCheck>();
        var mutations = new List<GuidePlannedMutation>();
        var (repository, selectorCheck) = ResolveRepository(repositorySelector);
        if (selectorCheck is not null)
        {
            blockers.Add(selectorCheck);
            return (GuideMutations.Prepare("source unbind", mutations, blockers), blockers);
        }

        var (ledger, ledgerIssue) = LoadLedger();
        if (ledgerIssue is not null)
        {
            blockers.Add(ledgerIssue);
        }

        var current = ledger?.Bindings.GetValueOrDefault(repository);
        if (current is null)
        {
            checks.Add(Check(SourceCheckId(repository), GuideCheckStatus.Warning,
                $"{repository} has no global source binding.", null));
        }
        else
        {
            var next = new Dictionary<string, GuideSourceBinding>(ledger!.Bindings, StringComparer.Ordinal);
            next.Remove(repository);
            if (next.Count == 0)
            {
                GuideMutations.AddLocalDelete(
                    "source.bindings.remove", LedgerFile, $"Remove the last recorded source binding ({repository}).", mutations);
            }
            else
            {
                GuideMutations.AddLocalWrite(
                    "source.bindings.write",
                    LedgerFile,
                    GuidePlanning.JsonBytes(new GuideSourceBindingLedger(GuideSourceBindingLedger.CurrentSchemaVersion, next)),
                    $"Remove the global {repository} source binding.",
                    mutations);
            }
        }

        var prepared = GuideMutations.Prepare("source unbind", mutations, [.. checks, .. blockers]);
        return (prepared, blockers);
    }

    /// <summary>
    /// Shared tail of the preview/apply flow: digest gate, blocker gate, no-op gate, then the
    /// local file mutations. Bind re-verifies the proposed checkout under the lock before
    /// writing, so a checkout that moved after preview fails closed.
    /// </summary>
    private async Task<GuideReport> CompleteAsync(
        GuidePreparedPlan prepared,
        IReadOnlyList<GuideCheck> blockers,
        string? expectedDigest,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        var operation = prepared.PublicPlan.Operation;
        var checks = prepared.PublicPlan.Checks.ToList();
        if (expectedDigest is null)
        {
            return Report(operation, checks, "Preview generated; no changes were made.",
                ["Review the plan and repeat with --apply --plan-digest."], prepared.PublicPlan);
        }

        if (expectedDigest != prepared.PublicPlan.PlanDigest)
        {
            checks.Add(Check("plan.digest", GuideCheckStatus.Error, "Approved plan digest is stale or missing.",
                "Preview and approve current state."));
            return Report(operation, checks, "No changes applied.", ["Preview again."], prepared.PublicPlan);
        }

        if (blockers.Count > 0)
        {
            return Report(operation, checks, "Blockers prevented apply.", ["Resolve errors and preview again."], prepared.PublicPlan);
        }

        if (prepared.PublicPlan.IsNoOp)
        {
            checks.Add(Check("plan.applied", GuideCheckStatus.Ok, "The approved plan was already satisfied; no changes were needed."));
            return Report(operation, checks, "Nothing to apply.", [], prepared.PublicPlan with { Applied = true });
        }

        progress?.Report(new GuidePhase("apply.step", $"Applying {prepared.Mutations.Count} source ledger change(s)…"));
        foreach (var mutation in prepared.Mutations)
        {
            await GuideMutations.ApplyLocalFileAsync((GuideLocalFileMutation)mutation, cancellationToken);
        }

        checks.Add(Check("plan.applied", GuideCheckStatus.Ok, "The approved plan was applied."));
        return Report(operation, checks, "Approved plan applied.", ["Run guide source list to confirm."],
            prepared.PublicPlan with { Applied = true });
    }

    internal GuideSourceBinding VerifyLocalBinding(string repository, string sourcePath, string? exactRef)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        if (!Directory.Exists(fullPath))
        {
            throw new GuideSourceException($"Source directory does not exist: {fullPath}.");
        }

        var git = _git.Describe(fullPath)
                   ?? throw new GuideSourceException($"Cannot identify an exact Git commit for {fullPath}.");
        if (!string.Equals(git.CanonicalRemote, repository, StringComparison.OrdinalIgnoreCase))
        {
            throw new GuideSourceException(
                $"{fullPath} is not a canonical {repository} checkout.",
                $"Clone {repository} and point the binding at that checkout.");
        }

        var resolvedRef = exactRef;
        if (!string.IsNullOrEmpty(resolvedRef) && !GuideGitProbe.IsCommit(resolvedRef.ToLowerInvariant()))
        {
            var tagCommit = _git.ResolveTagCommit(git.Root, resolvedRef);
            if (tagCommit is null)
            {
                throw new GuideSourceException($"Source ref {resolvedRef} is not a 40-character commit or an existing Git tag.");
            }

            // The binding records the human-chosen tag as its ref and proves it against the
            // resolved commit, mirroring the pinned resolver's exact-tag provenance.
            resolvedRef = tagCommit;
        }

        if (resolvedRef is not null && !string.Equals(git.Commit, resolvedRef.ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new GuideSourceException($"Source commit {git.Commit} does not match required commit {resolvedRef}.");
        }

        var isTag = exactRef is not null && !GuideGitProbe.IsCommit(exactRef.ToLowerInvariant());
        return new GuideSourceBinding(
            repository,
            exactRef ?? git.Commit,
            git.Commit,
            git.Root,
            isTag ? "exact-tag" : "exact-commit",
            "local-git");
    }

    internal GuideSourceBinding ResolveCachedBinding(string repository, string? exactRef, string? resolverPath)
    {
        if (string.IsNullOrWhiteSpace(exactRef))
        {
            throw new GuideSourceException("Cached source binding requires --source-ref <exact-ref>.",
                "Pass a 40-character commit or an existing tag of the pinned resolver cache.");
        }

        var activeResolver = _resolver;
        if (activeResolver is null)
        {
            var discovered = InspectDependencySourceResolver.Discover(resolverPath);
            if (discovered is null)
            {
                throw new GuideSourceException(
                    "inspect-dependency-source is not installed or MONICA_INSPECT_SOURCE_CLI is unset.",
                    "Install the pinned inspect-dependency-source agent skill, or bind a local checkout with --source-path.");
            }

            activeResolver = discovered;
        }

        return activeResolver.Resolve(repository, exactRef);
    }

    internal GuideSourceObservation Observe(GuideSourceBinding binding)
    {
        var warnings = new List<GuideCheck>();
        if (!Directory.Exists(binding.SourcePath))
        {
            warnings.Add(Check(SourceWarningId(binding.Repository, "unavailable"), GuideCheckStatus.Warning,
                $"Stored source path is unavailable: {binding.SourcePath}."));
            return new GuideSourceObservation(
                binding.Repository, binding.Ref, binding.Commit, binding.SourcePath,
                null, "missing", null, null, warnings);
        }

        var git = _git.Describe(binding.SourcePath);
        string health;
        if (git is null)
        {
            health = "invalid";
            warnings.Add(Check(SourceWarningId(binding.Repository, "identity"), GuideCheckStatus.Warning,
                $"Stored path is not a readable Git checkout: {binding.SourcePath}."));
        }
        else if (!string.Equals(git.CanonicalRemote, binding.Repository, StringComparison.OrdinalIgnoreCase))
        {
            health = "invalid";
            warnings.Add(Check(SourceWarningId(binding.Repository, "identity"), GuideCheckStatus.Warning,
                $"Stored path is not a canonical {binding.Repository} checkout."));
        }
        else if (!string.Equals(git.Commit, binding.Commit, StringComparison.Ordinal))
        {
            health = "moved";
            warnings.Add(Check(SourceWarningId(binding.Repository, "moved"), GuideCheckStatus.Warning,
                $"Checkout moved from {binding.Commit} to {git.Commit}."));
        }
        else
        {
            health = "available";
        }

        if (git?.Dirty == true)
        {
            warnings.Add(Check(SourceWarningId(binding.Repository, "dirty"), GuideCheckStatus.Warning,
                "Checkout contains local changes; the binding remains a lookup locator only."));
        }

        return new GuideSourceObservation(
            binding.Repository, binding.Ref, binding.Commit, binding.SourcePath,
            git?.Commit, health, git?.Dirty, git?.CanonicalRemote, warnings);
    }

    internal (GuideSourceBindingLedger? Ledger, GuideCheck? Issue) LoadLedger()
    {
        if (!File.Exists(LedgerFile))
        {
            return (null, null);
        }

        try
        {
            var ledger = JsonSerializer.Deserialize<GuideSourceBindingLedger>(
                File.ReadAllBytes(LedgerFile), GuidePlanning.JsonOptions);
            if (ledger is null || ledger.SchemaVersion != GuideSourceBindingLedger.CurrentSchemaVersion)
            {
                return (null, Check("source.bindings.ledger", GuideCheckStatus.Error,
                    $"The source binding ledger schema {ledger?.SchemaVersion ?? 0} predates this version.",
                    "Remove or repair state/source-bindings.json, then bind sources again."));
            }

            return (ledger, null);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            return (null, Check("source.bindings.ledger", GuideCheckStatus.Error,
                $"The source binding ledger is unreadable: {exception.Message}",
                "Remove or repair state/source-bindings.json, then bind sources again."));
        }
    }

    internal (string Repository, GuideCheck? Error) ResolveRepository(string selector)
    {
        var trimmed = selector.Trim();
        foreach (var (repository, aliases) in DeclaredRepositories())
        {
            if (string.Equals(trimmed, repository, StringComparison.OrdinalIgnoreCase)
                || aliases.Any(alias => string.Equals(trimmed, alias, StringComparison.OrdinalIgnoreCase)))
            {
                return (repository, null);
            }
        }

        return (trimmed, Check("source.repository", GuideCheckStatus.Error,
            $"Unknown source repository '{trimmed}'.",
            $"Known repositories: {string.Join(", ", DeclaredRepositories().Select(static item => item.Repository))}."));
    }

    private IReadOnlyList<(string Repository, string[] Aliases)> DeclaredRepositories()
        => _catalog?.SourceRepositories is { Count: > 0 } repositories
            ? repositories.Values
                .Select(static item => (item.Repository, item.Aliases.ToArray()))
                .OrderBy(static item => item.Repository, StringComparer.Ordinal)
                .ToArray()
            : KNOWN_REPOSITORIES.Select(static item => (item.Repository, item.Aliases)).ToArray();

    internal static string SourceCheckId(string repository)
        => $"source.binding.{repository}";

    private static string SourceWarningId(string repository, string warning)
        => $"source.binding.{repository}.{warning}";

    private static GuideCheck BindingCheck(GuideSourceObservation observation)
    {
        var status = observation.PathHealth == "available" && observation.Dirty != true
            ? GuideCheckStatus.Ok
            : GuideCheckStatus.Warning;
        var details = new Dictionary<string, string>
        {
            ["path"] = observation.SourcePath,
            ["ref"] = observation.StoredRef,
            ["commit"] = observation.StoredCommit
        };
        if (observation.ObservedCommit is not null)
        {
            details["observedCommit"] = observation.ObservedCommit;
        }

        return Check(SourceCheckId(observation.Repository), status,
            $"{observation.Repository}: {observation.PathHealth} @ {observation.SourcePath}",
            null, details);
    }

    private static IReadOnlyDictionary<string, GuideSourceBinding> EmptyBindings()
        => new Dictionary<string, GuideSourceBinding>();

    private static GuideCheck Check(string id, GuideCheckStatus status, string message, string? remediation = null,
        IReadOnlyDictionary<string, string>? details = null)
        => new(id, status, message, remediation, details);

    private static GuideReport Report(string command, IReadOnlyList<GuideCheck> checks, string message,
        IReadOnlyList<string> nextActions, GuidePlan? plan = null)
        => new(GuideContractVersions.CURRENT, AgentGuideInfo.CurrentVersion(), StatusOf(checks), checks,
            new GuideSummary(command, message, DateTimeOffset.UtcNow, nextActions), plan);

    private static GuideStatus StatusOf(IEnumerable<GuideCheck> checks)
    {
        var statuses = checks.Select(static item => item.Status).ToArray();
        return statuses.Contains(GuideCheckStatus.Error) ? GuideStatus.Error
            : statuses.Contains(GuideCheckStatus.Warning) ? GuideStatus.Warning : GuideStatus.Ready;
    }
}

/// <summary>
/// The pinned external <c>inspect-dependency-source</c> agent skill: resolves exact cached
/// first-party source checkouts. The guide discovers its installed copy in the standard agent
/// skill roots or through <c>MONICA_INSPECT_SOURCE_CLI</c>; it never fetches a substitute.
/// </summary>
public interface IGuideSourceResolver
{
    /// <summary>Resolves one exact ref to a verified cached checkout; throws on any drift.</summary>
    GuideSourceBinding Resolve(string repository, string exactRef);
}

public sealed class GuideSourceException(string message, string? remediation = null) : Exception(message)
{
    public string? Remediation { get; } = remediation;
}

internal sealed record GuideSourceBindingLedger(
    int SchemaVersion,
    IReadOnlyDictionary<string, GuideSourceBinding> Bindings)
{
    public const int CurrentSchemaVersion = 1;
}

internal sealed class InspectDependencySourceResolver(string scriptPath, string pythonCommand) : IGuideSourceResolver
{
    private static readonly TimeSpan COMMAND_TIMEOUT = TimeSpan.FromSeconds(120);

    // The pinned resolver CLI publishes a snake_case JSON contract.
    private static readonly JsonSerializerOptions RESOLVER_JSON = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static InspectDependencySourceResolver? Discover(string? explicitPath)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            candidates.Add(explicitPath);
        }

        var environment = Environment.GetEnvironmentVariable("MONICA_INSPECT_SOURCE_CLI");
        if (!string.IsNullOrWhiteSpace(environment))
        {
            candidates.Add(environment);
        }

        candidates.AddRange(
        [
            Path.Combine(home, ".agents", "skills", "inspect-dependency-source", "scripts", "inspect_dependency_source.py"),
            Path.Combine(home, ".claude", "skills", "inspect-dependency-source", "scripts", "inspect_dependency_source.py"),
            Path.Combine(home, ".codex", "skills", "inspect-dependency-source", "scripts", "inspect_dependency_source.py")
        ]);
        var script = candidates.FirstOrDefault(File.Exists);
        if (script is null)
        {
            return null;
        }

        var python = new[] { Environment.GetEnvironmentVariable("MONICA_GUIDE_PYTHON"), "python3", "python" }
            .FirstOrDefault(static command => !string.IsNullOrWhiteSpace(command));
        return python is null ? null : new InspectDependencySourceResolver(script, python);
    }

    public GuideSourceBinding Resolve(string repository, string exactRef)
    {
        var output = RunResolve(repository, exactRef);
        Payload payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(output, RESOLVER_JSON)
                      ?? throw new GuideSourceException("inspect-dependency-source returned an empty resolution.");
        }
        catch (JsonException exception)
        {
            throw new GuideSourceException(
                $"inspect-dependency-source returned invalid JSON: {exception.Message}");
        }

        if (!string.Equals(payload.Status, "ok", StringComparison.Ordinal)
            || !string.Equals(payload.VerificationState, "verified", StringComparison.Ordinal))
        {
            throw new GuideSourceException(
                $"Cached {repository} source does not have verified exact provenance ({payload.Status}/{payload.VerificationState}).");
        }

        if (payload.ResolutionKind is not ("exact-commit" or "exact-tag"))
        {
            throw new GuideSourceException(
                $"Cached {repository} source resolution kind '{payload.ResolutionKind}' is not exact.");
        }

        var commit = payload.Artifact?.ActualCommit;
        if (string.IsNullOrWhiteSpace(commit) || !GuideGitProbe.IsCommit(commit.ToLowerInvariant()))
        {
            throw new GuideSourceException("Cached source result has no exact observed commit.");
        }

        commit = commit.ToLowerInvariant();
        if (GuideGitProbe.IsCommit(exactRef.ToLowerInvariant())
            && !string.Equals(commit, exactRef.ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new GuideSourceException($"Cached source resolved {commit}, not requested commit {exactRef}.");
        }

        if (!string.Equals(payload.Repository?.CanonicalName, repository, StringComparison.OrdinalIgnoreCase))
        {
            throw new GuideSourceException(
                $"Resolved source is {payload.Repository?.CanonicalName ?? "unknown"}, not {repository}.");
        }

        var sourcePath = payload.SourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !Path.IsPathRooted(sourcePath) || !Directory.Exists(sourcePath))
        {
            throw new GuideSourceException("Cached source path is not an available absolute directory.");
        }

        return new GuideSourceBinding(
            repository,
            payload.Artifact?.Ref ?? exactRef,
            commit,
            Path.GetFullPath(sourcePath),
            payload.ResolutionKind!,
            "inspect-dependency-source");
    }

    private string RunResolve(string repository, string exactRef)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = pythonCommand,
                Arguments = $"\"{scriptPath}\" resolve {repository} --ref \"{exactRef}\" --json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
            {
                throw new GuideSourceException("inspect-dependency-source could not start.");
            }

            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit((int)COMMAND_TIMEOUT.TotalMilliseconds))
            {
                process.Kill();
                throw new GuideSourceException(
                    $"inspect-dependency-source timed out after {(int)COMMAND_TIMEOUT.TotalMilliseconds} ms while resolving cached {repository} source.");
            }

            if (process.ExitCode != 0)
            {
                throw new GuideSourceException(
                    $"Exact cached {repository} source is unavailable (exit {process.ExitCode}).");
            }

            return output.GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new GuideSourceException(
                $"The inspect-dependency-source runtime ('{pythonCommand}') is unavailable.");
        }
    }

    private sealed class Payload
    {
        public string? Status { get; set; }
        public string? VerificationState { get; set; }
        public string? ResolutionKind { get; set; }
        public string? SourcePath { get; set; }
        public RepositoryInfo? Repository { get; set; }
        public ArtifactInfo? Artifact { get; set; }
    }

    private sealed class RepositoryInfo
    {
        public string? CanonicalName { get; set; }
    }

    private sealed class ArtifactInfo
    {
        public string? ActualCommit { get; set; }
        public string? ExpectedCommit { get; set; }
        public string? Ref { get; set; }
    }
}
