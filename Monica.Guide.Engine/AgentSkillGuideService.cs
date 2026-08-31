using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Monica.Guide;

/// <summary>Preview-first guide engine for directory-level skill catalog management.</summary>
/// <remarks>
/// The guide owns one concern: replacing and removing catalog-owned skill directories in agent
/// skill roots (Windows, WSL, Linux, and macOS). Anything inside a catalog-named skill
/// directory is guide-owned and replaced wholesale; third-party skills are never touched. The
/// engine is generic over an <see cref="AgentProductDefinition"/>: product identity, entry
/// executable, routes, hosts, release pins, and update feeds are data, so one engine serves
/// every product and ownership lives in one unified ledger shared across products.
/// </remarks>
public sealed class AgentGuideService : IAgentGuideService, IDisposable
{
    private readonly AgentProductDefinition _definition;
    private readonly GuidePaths _enginePaths;
    private readonly AgentProductPaths _productPaths;
    private readonly IGuideEnvironmentRuntime _runtime;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _applicationDirectory;
    private readonly Func<int, CancellationToken, Task<bool>> _portProbe;
    private readonly Func<string> _currentVersion;
    private readonly string? _currentAssemblyVersion;
    private HashSet<string>? _inspectionOwnedNames;
    private IReadOnlyDictionary<string, GuideSkillAlias>? _inspectionAliases;

    public AgentGuideService(
        AgentProductDefinition definition,
        GuidePaths? enginePaths = null,
        AgentProductPaths? productPaths = null,
        IGuideHostEnvironment? host = null,
        HttpClient? httpClient = null,
        string? applicationDirectory = null,
        Func<string>? currentVersion = null,
        string? currentAssemblyVersion = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        host ??= new GuideHostEnvironment();
        _enginePaths = enginePaths ?? GuidePaths.ForCurrentUser(host);
        _productPaths = productPaths ?? AgentProductPaths.ForCurrentUser(definition, host);
        _runtime = new GuideEnvironmentRuntime(host);
        // Every guide HTTP call is a loopback runtime probe; a system proxy must never
        // intercept it, and failed probes should surface quickly when nothing listens.
        _http = httpClient ?? new HttpClient(
            new SocketsHttpHandler { UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(5) })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _ownsHttp = httpClient is null;
        _applicationDirectory = Path.GetFullPath(applicationDirectory ?? AppContext.BaseDirectory);
        // Tests inject a deterministic probe so plans never depend on real loopback
        // listeners (or on the user's running product) while staying fast.
        _portProbe = IsPortListeningAsync;
        _currentVersion = currentVersion ?? AgentGuideInfo.CurrentVersion;
        _currentAssemblyVersion = currentAssemblyVersion;
    }

    internal AgentGuideService(
        AgentProductDefinition definition,
        GuidePaths enginePaths,
        AgentProductPaths productPaths,
        IGuideEnvironmentRuntime runtime,
        HttpClient? httpClient,
        string applicationDirectory,
        Func<int, CancellationToken, Task<bool>>? portProbe = null,
        Func<string>? currentVersion = null,
        string? currentAssemblyVersion = null)
    {
        _definition = definition;
        _enginePaths = enginePaths;
        _productPaths = productPaths;
        _runtime = runtime;
        _http = httpClient ?? new HttpClient(
            new SocketsHttpHandler { UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(5) })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _ownsHttp = httpClient is null;
        _applicationDirectory = Path.GetFullPath(applicationDirectory);
        _portProbe = portProbe ?? IsPortListeningAsync;
        _currentVersion = currentVersion ?? AgentGuideInfo.CurrentVersion;
        _currentAssemblyVersion = currentAssemblyVersion;
    }

    private string CurrentVersion => _currentVersion();
    private string Product => _definition.DisplayName;
    private string StagingPrefix => $".{_definition.SkillNamespacePrefix.TrimEnd('-')}-staging-";

    public Task<GuideReport> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var checks = new List<GuideCheck>
        {
            Check("product.version", GuideCheckStatus.Ok, $"{Product} {CurrentVersion}."),
            Check("product.data-root", GuideCheckStatus.Ok, "The user-local product data root is resolvable.")
        };
        try
        {
            var release = ObserveSelfRelease(CurrentVersion);
            checks.Add(Check("distribution.kind", release.Manifest is null ? GuideCheckStatus.Warning : GuideCheckStatus.Ok,
                release.Manifest is null ? "Source/development layout detected." : $"{release.Manifest.RuntimeIdentifier} release manifest verified.",
                release.Manifest is null ? "Use a complete packaged release for end-user installation." : null));
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or ArgumentException)
        {
            checks.Add(Check(
                "distribution.kind",
                GuideCheckStatus.Error,
                $"The release manifest is unreadable or invalid: {exception.Message}",
                $"Re-extract one complete checksum-verified {Product} release."));
        }
        var tagline = _definition.Tagline ?? "one governed agent product.";
        return Task.FromResult(Report("overview", checks, $"{Product} {tagline}",
            ["Run 'guide status' to inspect setup.", "Run 'guide doctor' to verify the live local service."]));
    }

    public async Task<GuideReport> GetStatusAsync(
        GuideInspectRequest? request = null,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ReportPhase(progress, "status.state", "Reading guide state…");
        var checks = new List<GuideCheck>();
        ProductGuideState? state = null;
        string? stateIncompatibility = null;
        try
        {
            state = LoadProductState(out stateIncompatibility);
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            checks.Add(Check(
                "configuration.state",
                GuideCheckStatus.Error,
                $"The guide ownership ledger is unreadable or invalid: {exception.Message}",
                "Keep the corrupt ledger for inspection, then unconfigure or restore it from a trusted backup."));
        }
        if (state is null)
        {
            if (checks.All(static check => check.Id != "configuration.state"))
            {
                checks.Add(Check(
                    "configuration.state",
                    GuideCheckStatus.Warning,
                    stateIncompatibility ?? $"No guide configuration is recorded for {Product}.",
                    stateIncompatibility is null
                        ? "Preview guide configure."
                        : "Preview guide configure once to re-record the installed skill targets."));
            }
        }
        else
        {
            checks.Add(Check("configuration.state", GuideCheckStatus.Ok, "Guide ownership state is readable."));
            checks.Add(Check("program.executable",
                File.Exists(state.ExecutablePath) ? GuideCheckStatus.Ok : GuideCheckStatus.Error,
                File.Exists(state.ExecutablePath) ? "The configured executable exists." : "The configured executable is missing.",
                File.Exists(state.ExecutablePath) ? null : "Restore the recorded release or configure a complete replacement release."));
            try
            {
                ReportPhase(progress, "status.release", "Verifying the configured release bundle…");
                // The configured bundle is validated against the version recorded in the guide
                // state, not the running binary, so a newer CLI can still audit an older install.
                var release = GuideReleaseMetadata.Observe(state.BundleRoot, state.ProductVersion, _definition);
                var releaseMatches = release.Manifest is null
                    ? state.ReleaseManifestDigest == ReleaseObservation.Development.ManifestDigest
                    : release.ManifestDigest == state.ReleaseManifestDigest
                      && release.Manifest.ProductVersion == state.ProductVersion;
                checks.Add(Check("release.identity", releaseMatches ? GuideCheckStatus.Ok : GuideCheckStatus.Error,
                    releaseMatches ? "The configured bundle still matches its recorded release identity." : "The configured bundle no longer matches its recorded manifest or version.",
                    releaseMatches ? null : "Do not mix files across releases; configure one complete immutable bundle."));
            }
            catch (Exception exception)
            {
                checks.Add(Check("release.identity", GuideCheckStatus.Error, exception.Message, "Restore or re-extract one complete release."));
            }

            var filteredInstallations = FilterInstallations(state.Installations, request?.Environments);
            if (request?.Environments is { Count: > 0 })
            {
                foreach (var environment in request.Environments.Where(environment =>
                             !filteredInstallations.Any(item => EqEnv(item.Environment, environment))))
                {
                    checks.Add(Check($"environment.{environment.Selector}.configured", GuideCheckStatus.Warning,
                        $"No guide configuration is recorded for {environment.Selector}.",
                        "Preview configure for the selected environment."));
                }
            }

            // Independent installation inspections run concurrently; their checks are
            // appended in the recorded order so report output stays deterministic.
            var installationChecks = await Task.WhenAll(filteredInstallations.Select(installation =>
                InspectInstallationAsync(installation, progress, cancellationToken)));
            foreach (var installationCheckList in installationChecks)
            {
                checks.AddRange(installationCheckList);
            }
        }

        if (_definition.ServesLoopback)
        {
            try
            {
                var serverConfiguration = GuideProductConfiguration.Load(_definition, _productPaths);
                var agreesWithState = state is null || state.BaseAddress is null
                                      || serverConfiguration.Port == state.BaseAddress.Port;
                checks.Add(Check(
                    "server.configuration",
                    agreesWithState ? GuideCheckStatus.Ok : GuideCheckStatus.Warning,
                    state is null
                        ? $"Serve configuration is readable and selects loopback port {serverConfiguration.Port}."
                        : agreesWithState
                            ? $"Serve port {serverConfiguration.Port} matches guide ownership state."
                            : "Persisted server configuration and guide ownership state disagree.",
                    agreesWithState ? null : "Preview configure to reconcile the serve port."));
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
            {
                checks.Add(Check(
                    "server.configuration",
                    GuideCheckStatus.Error,
                    $"The server configuration is unreadable or invalid: {exception.Message}",
                    "Keep the corrupt local file for inspection, then preview configure to replace it safely."));
            }
        }

        var inspectionEnvironments = (request?.Environments is { Count: > 0 }
                ? request.Environments
                : _runtime.DetectEnvironments()
                    .Concat(state?.Installations.Select(static installation => installation.Environment) ?? []))
            .DistinctBy(static environment => environment.Selector, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (inspectionEnvironments.Length > 0)
        {
            ReportPhase(progress, "status.environments", "Detecting installed agent hosts…");
        }
        // Host detection shells out per candidate command, so environments are scanned
        // concurrently and the ordered results drive the deterministic check output below.
        var detectedByEnvironment = await Task.WhenAll(inspectionEnvironments.Select(environment =>
            Task.Run(() => DetectHostsAsync(environment, progress, cancellationToken), cancellationToken)));
        foreach (var environmentChecks in detectedByEnvironment)
        {
            checks.AddRange(environmentChecks);
        }

        return Report("status", checks, "Configuration status inspected.", ["Run 'guide doctor' to verify live service health."]);
    }

    public async Task<GuideHealthReport> DiagnoseAsync(
        GuideInspectRequest? request = null,
        Uri? baseAddress = null,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(request, progress, cancellationToken);
        var checks = status.Checks.ToList();
        try
        {
            _ = LoadProductState();
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            checks.Add(Check(
                "runtime.configuration-state",
                GuideCheckStatus.Error,
                $"Runtime endpoint selection could not use the corrupt guide state: {exception.Message}",
                "Repair or safely replace the guide ledger; the default loopback endpoint is probed meanwhile."));
        }
        var probe = await ProbeRuntimeAsync(baseAddress, progress, cancellationToken);
        checks.AddRange(probe.Checks);
        var aggregate = StatusOf(checks);
        return new GuideHealthReport(
            GuideContractVersions.CURRENT,
            CurrentVersion,
            aggregate,
            checks,
            Summary("doctor", aggregate == GuideStatus.Ready ? "Runtime and configuration are ready." : "Doctor found items requiring attention.",
                aggregate == GuideStatus.Ready ? ["Open the product UI and complete one small governed route."] : ["Follow each check remediation."]));
    }

    public async Task<GuideHealthReport> ProbeRuntimeAsync(
        Uri? baseAddress = null,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!_definition.ServesLoopback)
        {
            return new GuideHealthReport(
                GuideContractVersions.CURRENT,
                CurrentVersion,
                GuideStatus.Ready,
                [Check("runtime.absent", GuideCheckStatus.Ok, $"{Product} runs no local service; runtime probes do not apply.")],
                Summary("runtime", $"{Product} runs no local service.", []));
        }

        var checks = new List<GuideCheck>();
        ProductGuideState? configuredState = null;
        try
        {
            configuredState = LoadProductState();
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            // Endpoint selection falls back to the persisted/default loopback address.
        }
        var defaultAddress = new Uri($"http://localhost:{GuideProductConfiguration.Load(_definition, _productPaths).Port}/");
        var baseUri = NormalizeLoopback(baseAddress ?? configuredState?.BaseAddress ?? defaultAddress);
        // The recorded installation is the authority on which release should be running; a
        // foreign installer doctering another product validates that install, not itself.
        var expectedDirectory = configuredState?.BundleRoot ?? _applicationDirectory;
        var expectedVersion = configuredState?.ProductVersion ?? CurrentVersion;
        ReportPhase(progress, "runtime.release", "Observing the local release identity…");
        ReleaseObservation expectedRelease;
        try
        {
            expectedRelease = ObserveSelfRelease(expectedVersion, expectedDirectory);
        }
        catch (Exception exception)
        {
            expectedRelease = ReleaseObservation.Development;
            checks.Add(Check(
                "runtime.expected-release",
                GuideCheckStatus.Error,
                exception.Message,
                "Restore the complete guide release before diagnosing the running product."));
        }
        if (expectedRelease.Manifest?.RequiredRuntime is { } requiredRuntime)
        {
            ReportPhase(progress, "runtime.aspnet", "Checking the installed ASP.NET Core shared runtime…");
            checks.Add(ObserveAspNetRuntime(requiredRuntime));
        }
        ReportPhase(progress, "runtime.healthz", "Probing the loopback health endpoint…");
        await ProbeHealthAsync(baseUri, expectedRelease, checks, cancellationToken);
        if (_definition.DefaultRoutes.TryGetValue("ui", out var uiPath))
        {
            ReportPhase(progress, "runtime.ui", "Probing the product UI…");
            await ProbeGetAsync("runtime.ui", Combine(baseUri, uiPath), checks, cancellationToken);
        }
        if (_definition.McpPath is not null && _definition.McpServerName is not null)
        {
            ReportPhase(progress, "runtime.mcp", "Probing the machine API handshake, catalog, and one read-only tool call…");
            await ProbeMcpAsync(
                AsDirectLoopback(Combine(baseUri, _definition.McpPath)),
                expectedRelease,
                checks,
                cancellationToken);
        }
        var aggregate = StatusOf(checks);
        return new GuideHealthReport(
            GuideContractVersions.CURRENT,
            CurrentVersion,
            aggregate,
            checks,
            Summary("runtime", aggregate == GuideStatus.Ready ? "Live loopback runtime is healthy." : "The live runtime probe found items requiring attention.",
                aggregate == GuideStatus.Ready ? [$"Agents call the machine API through the {_definition.McpServerName} operations skill helper scripts."] : ["Follow each check remediation."]));
    }

    public Task<GuideReport> PreviewConfigureAsync(
        GuideConfigureRequest request,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
        => ConfigureAsync(request, null, false, progress, cancellationToken);

    public Task<GuideReport> ApplyConfigureAsync(
        GuideConfigureRequest request,
        string expectedPlanDigest,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
        => ConfigureAsync(request, expectedPlanDigest, true, progress, cancellationToken);

    public Task<GuideReport> PreviewUnconfigureAsync(
        GuideUnconfigureRequest request,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
        => UnconfigureAsync(request, null, false, progress, cancellationToken);

    public Task<GuideReport> ApplyUnconfigureAsync(
        GuideUnconfigureRequest request,
        string expectedPlanDigest,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
        => UnconfigureAsync(request, expectedPlanDigest, true, progress, cancellationToken);

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    public IReadOnlyList<GuideInstallationView> ListRecordedInstallations()
        => (LoadProductState()?.Installations ?? [])
            .Select(static installation => new GuideInstallationView(
                installation.Environment,
                installation.Target,
                installation.TargetRoot,
                installation.Trees.Count,
                installation.WorkspaceRoot))
            .OrderBy(static installation => installation.Environment.Selector, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static installation => NormalizeRoot(installation.TargetRoot), StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private async Task<GuideReport> ConfigureAsync(
        GuideConfigureRequest request,
        string? expectedDigest,
        bool apply,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var guideLock = apply ? await AcquireLockAsync(cancellationToken) : null;
        var prepared = await BuildConfigurePlanAsync(request, progress, cancellationToken);
        return await CompleteAsync(prepared, expectedDigest, apply, progress, cancellationToken);
    }

    private async Task<GuideReport> UnconfigureAsync(
        GuideUnconfigureRequest request,
        string? expectedDigest,
        bool apply,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var guideLock = apply ? await AcquireLockAsync(cancellationToken) : null;
        var prepared = await BuildUnconfigurePlanAsync(request, progress, cancellationToken);
        return await CompleteAsync(prepared, expectedDigest, apply, progress, cancellationToken);
    }

    private async Task<GuidePreparedPlan> BuildConfigurePlanAsync(
        GuideConfigureRequest request,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        var checks = new List<GuideCheck>();
        var mutations = new List<GuidePlannedMutation>();
        ReportPhase(progress, "configure.state", "Reading guide state…");
        var ledger = LoadLedger(out var stateIncompatibility);
        var previous = FindProductState(ledger);
        if (stateIncompatibility is not null)
        {
            checks.Add(Check(
                "configuration.state",
                GuideCheckStatus.Warning,
                stateIncompatibility,
                "This configure re-records every installed skill target from the catalog."));
        }
        var executable = ResolveExecutable(request.ExecutablePath);
        checks.Add(Check("program.executable", File.Exists(executable) ? GuideCheckStatus.Ok : GuideCheckStatus.Error,
            File.Exists(executable) ? "Selected executable exists." : "Selected executable does not exist.",
            File.Exists(executable) ? null : $"Use {_definition.CurrentProgramEntryPoint ?? "a complete release"} from a complete release."));
        var bundleRoot = ResolveBundleRoot(executable, request.ReleaseManifestPath);
        if (request.ReleaseManifestPath is not null
            && !File.Exists(Path.GetFullPath(request.ReleaseManifestPath)))
        {
            checks.Add(Check("release.manifest.path", GuideCheckStatus.Error,
                "The explicitly selected release manifest does not exist.", "Select bundle-root/release-manifest.json."));
        }
        ReleaseObservation release;
        try
        {
            // Configure explicitly selects a bundle, so it is validated as a candidate release
            // and its version is recorded into the new guide state below.
            ReportPhase(progress, "configure.release", "Validating the selected release bundle…");
            release = GuideReleaseMetadata.Observe(bundleRoot, null, _definition);
            checks.Add(Check("release.manifest", release.Manifest is null ? GuideCheckStatus.Warning : GuideCheckStatus.Ok,
                release.Manifest is null ? "No release manifest found; development mode." : "Release manifest is valid."));
        }
        catch (Exception exception)
        {
            release = ReleaseObservation.Development;
            checks.Add(Check("release.manifest", GuideCheckStatus.Error, exception.Message, "Re-extract a complete release."));
        }

        var skillsRoot = Path.GetFullPath(request.SkillsRootPath ?? Path.Combine(bundleRoot, "skills"));
        SkillCatalog? catalog = null;
        try
        {
            ReportPhase(progress, "configure.catalog", "Validating the exact skill catalog…");
            var catalogPath = Path.GetFullPath(request.SkillCatalogPath ?? Path.Combine(skillsRoot, "catalog.json"));
            catalog = GuideReleaseMetadata.LoadSkillCatalog(skillsRoot, Path.GetRelativePath(skillsRoot, catalogPath));
            checks.Add(Check("skills.catalog", GuideCheckStatus.Ok,
                $"Exact {catalog.SkillCount}-skill catalog validated."));
        }
        catch (Exception exception)
        {
            checks.Add(Check("skills.catalog", GuideCheckStatus.Error, exception.Message, "Keep the program tree and skills/ from one release."));
        }
        if (catalog is null)
        {
            // The catalog error above blocks apply; there is nothing further to plan.
            return Prepared("configure", mutations, checks, previous);
        }

        var (selected, plannedTargets, workspaceRoot) = ResolveInstallScope(request, catalog, previous, checks);
        if (selected is null || plannedTargets.Count == 0)
        {
            if (selected is not null && plannedTargets.Count == 0)
            {
                checks.Add(Check(
                    "configuration.empty",
                    GuideCheckStatus.Error,
                    "No skill target is selected and no installation is recorded for this user.",
                    "Select a workspace, or the shared catalog or Claude target for at least one environment, then preview again."));
            }

            return Prepared("configure", mutations, checks, previous);
        }
        CheckCrossProductOwnership(ledger, selected, checks);
        if (checks.Any(static check => check.Id == "skills.ownership" && check.Status == GuideCheckStatus.Error))
        {
            return Prepared("configure", mutations, checks, previous);
        }

        // Sources are read once per skill; every environment receives the same immutable bytes.
        var filesBySkill = LoadCatalogFiles(skillsRoot, catalog);
        var verificationDigests = selected.ToDictionary(
            skill => skill.Name,
            skill => ComputeVerificationDigest(
                filesBySkill[skill.Name].Select(file => (file.RelativePath, GuidePlanning.Sha256(file.Content)))),
            StringComparer.Ordinal);
        var installations = previous?.Installations.ToList() ?? [];
        foreach (var planned in plannedTargets)
        {
            var environment = planned.Environment;
            var target = planned.Target;
            var targetRoot = planned.TargetRoot;
            var targetName = TargetCheckName(target, targetRoot);
            ReportPhase(progress, $"configure.skills.{environment.Selector}.{targetName}",
                $"Planning the {targetName} skill projection in {environment.Selector} ({selected.Count} skills)…");
            var skillDirectories = selected
                .Select(skill => JoinEnvironmentPath(environment, targetRoot, skill.Name))
                .ToArray();
            // The install root itself anchors observation: global roots sit below the user
            // home, project roots below the workspace, and the containment guard applies
            // identically to both.
            var tree = await _runtime.ObserveTreeAsync(
                environment,
                targetRoot,
                [targetRoot, .. skillDirectories],
                [],
                [],
                cancellationToken);
            if (tree.Paths.TryGetValue(targetRoot, out var targetObservation) && targetObservation.Redirected)
            {
                checks.Add(Check(
                    $"skills.{environment.Selector}.{targetName}.target-redirection",
                    GuideCheckStatus.Error,
                    "The skill target root contains a symbolic link, junction, or other reparse point.",
                    "Replace redirected path components below the selected install root with ordinary directories before configuring."));
                continue;
            }
            var targetKind = tree.Paths.TryGetValue(targetRoot, out var targetKindObservation)
                ? targetKindObservation.Kind
                : null;
            if (targetKind is not null and not GuideFileSystemEntryKind.Directory)
            {
                checks.Add(Check(
                    $"skills.{environment.Selector}.{targetName}.target-kind",
                    GuideCheckStatus.Error,
                    "The skill target root exists but is not an ordinary directory.",
                    "Move the conflicting filesystem entry aside before configuring."));
                continue;
            }

            var blocked = false;
            foreach (var skill in selected)
            {
                var skillDirectory = JoinEnvironmentPath(environment, targetRoot, skill.Name);
                var redirected = tree.Paths.TryGetValue(skillDirectory, out var skillObservation)
                                 && skillObservation.Redirected;
                var skillKind = tree.Paths.TryGetValue(skillDirectory, out var skillKindObservation)
                    ? skillKindObservation.Kind
                    : null;
                if (!redirected && skillKind is null or GuideFileSystemEntryKind.Directory)
                {
                    continue;
                }

                blocked = true;
                checks.Add(Check(
                    $"skills.{environment.Selector}.{targetName}.{skill.Name}.target-kind",
                    GuideCheckStatus.Error,
                    redirected
                        ? $"The existing skill directory '{skill.Name}' is redirected."
                        : $"The existing skill entry '{skill.Name}' is not an ordinary directory.",
                    "Move the conflicting entry aside; the guide replaces whole skill directories only."));
            }
            if (blocked)
            {
                continue;
            }

            var stagingRoot = JoinEnvironmentPath(
                environment,
                targetRoot,
                $"{StagingPrefix}{Guid.NewGuid():N}");
            foreach (var skill in selected)
            {
                var files = filesBySkill[skill.Name];
                var action = new GuidePlanAction(
                    $"skills.{environment.Selector}.{targetName}.replace.{skill.Name}",
                    GuidePlanActionKind.ReplaceSkillDirectory,
                    JoinEnvironmentPath(environment, targetRoot, skill.Name),
                    null,
                    verificationDigests[skill.Name],
                    $"Replace the '{skill.Name}' skill directory ({files.Count} files) in {targetName}@{environment.Selector}.");
                mutations.Add(new GuideSkillReplace(action, environment, targetRoot, skill.Name, stagingRoot, files));
            }
            // A recorded installation may carry skills the new selection dropped; wholesale
            // replacement removes them so the target ends exactly at the selection's shape.
            var previousInstallation = previous?.Installations.FirstOrDefault(item =>
                EqEnv(item.Environment, environment)
                && SameRoot(item.TargetRoot, targetRoot));
            foreach (var staleTree in (previousInstallation?.Trees ?? [])
                         .Where(tree => selected.All(skill => skill.Name != tree.Name)))
            {
                var action = new GuidePlanAction(
                    $"skills.{environment.Selector}.{targetName}.remove-obsolete.{staleTree.Name}",
                    GuidePlanActionKind.DeleteSkillDirectory,
                    JoinEnvironmentPath(environment, targetRoot, staleTree.Name),
                    null,
                    null,
                    $"Delete the obsolete '{staleTree.Name}' skill directory in {targetName}@{environment.Selector}.");
                mutations.Add(new GuideSkillRemove(action, environment, targetRoot, [staleTree.Name]));
            }
            installations.RemoveAll(item => EqEnv(item.Environment, environment) && SameRoot(item.TargetRoot, targetRoot));
            installations.Add(new GuideSkillInstallation(
                environment,
                target,
                targetRoot,
                planned.WorkspaceRoot,
                selected
                    .Select(skill => new GuideSkillTree(skill.Name, verificationDigests[skill.Name]))
                    .ToArray()));
        }

        if (installations.Count == 0)
        {
            checks.Add(Check(
                "configuration.empty",
                GuideCheckStatus.Error,
                "No skill projection could be planned.",
                "Resolve the errors above, then preview again."));
            return Prepared("configure", mutations, checks, previous);
        }

        Uri? desiredBaseAddress = null;
        if (_definition.ServesLoopback)
        {
            if (request.BaseAddress is null)
            {
                checks.Add(Check("server.base-address", GuideCheckStatus.Error,
                    "The product serves a loopback endpoint but no base address was selected.",
                    "Select the loopback port to persist for application startup."));
                return Prepared("configure", mutations, checks, previous);
            }
            desiredBaseAddress = NormalizeLoopback(request.BaseAddress);
            var currentPort = previous?.BaseAddress?.Port
                              ?? GuideProductConfiguration.Load(_definition, _productPaths).Port;
            if (request.BaseAddress.Port != currentPort
                && await _portProbe(currentPort, cancellationToken))
            {
                checks.Add(Check("server.port.offline", GuideCheckStatus.Error,
                    $"{Product} is still listening on its currently configured port.",
                    $"Stop {Product} before changing the persisted serve port."));
            }
            if (request.BaseAddress.Port != currentPort
                && await _portProbe(request.BaseAddress.Port, cancellationToken))
            {
                checks.Add(Check(
                    "server.port.available",
                    GuideCheckStatus.Error,
                    $"The requested loopback port {request.BaseAddress.Port} is already in use.",
                    "Stop the process occupying the requested port or choose another loopback port before applying."));
            }
        }
        var desiredProductVersion = release.Manifest?.ProductVersion ?? CurrentVersion;
        var state = new ProductGuideState(
            _definition.ProductId,
            desiredProductVersion,
            executable,
            bundleRoot,
            release.ManifestDigest,
            desiredBaseAddress,
            installations.OrderBy(
                static item => $"{item.Environment.Selector}:{TargetCheckName(item.Target, item.TargetRoot)}:{item.TargetRoot}",
                StringComparer.OrdinalIgnoreCase).ToArray());
        if (workspaceRoot is not null)
        {
            AdoptWorkspaceRegistration(workspaceRoot, checks, mutations);
        }
        if (_definition.ServesLoopback && desiredBaseAddress is not null)
        {
            AddLocalWrite("server.configuration.write", _productPaths.ServerConfigurationFile,
                GuidePlanning.JsonBytes(new GuideServerConfiguration(GuideServerConfiguration.CurrentSchemaVersion, desiredBaseAddress.Port)),
                "Persist the configured localhost port for application startup.", mutations);
        }
        AddStateWrites(ledger, state, mutations);
        return Prepared("configure", mutations, checks, state);
    }

    private async Task<GuidePreparedPlan> BuildUnconfigurePlanAsync(
        GuideUnconfigureRequest request,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        ReportPhase(progress, "unconfigure.state", "Reading guide state…");
        var ledger = LoadLedger(out var stateIncompatibility);
        var state = FindProductState(ledger);
        if (state is null)
        {
            return Prepared("unconfigure", [],
                stateIncompatibility is null
                    ? [Check("configuration.state", GuideCheckStatus.Ok, "Configuration is already absent.")]
                    : [Check(
                        "configuration.state",
                        GuideCheckStatus.Warning,
                        stateIncompatibility,
                        "Preview guide configure once to re-record the installed skill targets, then unconfigure them.")],
                null);
        }

        var selectedTargets = request.Targets is { Count: > 0 } ? request.Targets.ToHashSet() : null;
        var selectedEnvironments = request.Environments is { Count: > 0 }
            ? request.Environments.Select(static item => item.Selector).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;
        string? selectedWorkspace = request.Workspace is null
            ? null
            : Path.GetFullPath(request.Workspace);
        var mutations = new List<GuidePlannedMutation>();
        var checks = new List<GuideCheck>();
        var selectedInstallations = state.Installations
            .Where(item => (selectedEnvironments is null || selectedEnvironments.Contains(item.Environment.Selector))
                           && (selectedTargets is null || selectedTargets.Contains(item.Target))
                           && (selectedWorkspace is null
                               || (item.WorkspaceRoot is not null
                                   && string.Equals(
                                       NormalizeRoot(item.WorkspaceRoot),
                                       NormalizeRoot(selectedWorkspace),
                                       StringComparison.OrdinalIgnoreCase))))
            .ToArray();
        if (selectedWorkspace is not null && selectedInstallations.Length == 0)
        {
            checks.Add(Check(
                "workspace.installations",
                GuideCheckStatus.Warning,
                $"No skill installation is recorded inside {selectedWorkspace}.",
                null));
        }
        foreach (var installation in selectedInstallations)
        {
            var selector = installation.Environment.Selector;
            var target = TargetCheckName(installation.Target, installation.TargetRoot);
            ReportPhase(progress, $"unconfigure.skills.{selector}.{target}",
                $"Planning {target} skill removal in {selector} ({installation.Trees.Count} skills)…");
            var skillDirectories = installation.Trees
                .Select(tree => JoinEnvironmentPath(installation.Environment, installation.TargetRoot, tree.Name))
                .ToArray();
            var tree = await _runtime.ObserveTreeAsync(
                installation.Environment,
                installation.TargetRoot,
                skillDirectories,
                [],
                [],
                cancellationToken);
            var blocked = false;
            foreach (var skillTree in installation.Trees)
            {
                var skillDirectory = JoinEnvironmentPath(installation.Environment, installation.TargetRoot, skillTree.Name);
                if (tree.Paths.TryGetValue(skillDirectory, out var observation) && observation.Redirected)
                {
                    blocked = true;
                    checks.Add(Check(
                        $"skills.{selector}.{target}.{skillTree.Name}.redirection",
                        GuideCheckStatus.Error,
                        $"The installed skill directory '{skillTree.Name}' is redirected; removal was refused.",
                        "Restore ordinary directories and inspect the redirected destination manually."));
                }
            }
            if (blocked)
            {
                continue;
            }

            var action = new GuidePlanAction(
                $"skills.{selector}.{target}.remove",
                GuidePlanActionKind.DeleteSkillDirectory,
                installation.TargetRoot,
                null,
                null,
                $"Delete the {installation.Trees.Count} {Product} skill directories ({string.Join(", ", installation.Trees.Select(static tree => tree.Name))}) from {target}@{selector}.");
            mutations.Add(new GuideSkillRemove(action, installation.Environment, installation.TargetRoot,
                installation.Trees.Select(static tree => tree.Name).ToArray()));
        }
        var retainedInstallations = state.Installations.Except(selectedInstallations).ToArray();
        var next = retainedInstallations.Length == 0
            ? null
            : state with { Installations = retainedInstallations };
        if (next is null)
        {
            AddLocalDelete("configuration.state.remove", GuidedLedgerRemovalDescription, mutations, ledger);
            AddLocalDelete("installation.locator.remove", _productPaths.InstallationLocatorFile, "Remove the bootstrap locator.", mutations);
            if (_definition.ServesLoopback)
            {
                AddLocalDelete("server.configuration.remove", _productPaths.ServerConfigurationFile, "Return future serve runs to the default loopback port.", mutations);
            }
        }
        else AddStateWrites(ledger, next, mutations);
        return Prepared("unconfigure", mutations, checks, next);
    }

    private const string GuidedLedgerRemovalDescription = "Remove the product's ownership records from the unified ledger.";

    /// <summary>Applies directory-level mutations: stage new trees, swap per skill, then local files.</summary>
    private async Task ApplyAsync(
        IReadOnlyList<GuidePlannedMutation> mutations,
        ProductGuideState? plannedState,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        var preparedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stagingRoots = new List<(GuideEnvironment Environment, string Path)>();
        try
        {
            for (var index = 0; index < mutations.Count; index++)
            {
                var mutation = mutations[index];
                ReportPhase(progress, "apply.step",
                    $"Applying step {index + 1} of {mutations.Count}: {mutation.PublicAction.Description}",
                    index + 1,
                    mutations.Count);
                switch (mutation)
                {
                    case GuideSkillReplace replace:
                        await PrepareStagingRootAsync(replace.Environment, replace.TargetRoot, replace.StagingRoot, preparedTargets, stagingRoots, cancellationToken);
                        var stagingSkill = JoinEnvironmentPath(replace.Environment, replace.StagingRoot, replace.SkillName);
                        await _runtime.WriteFilesAsync(
                            replace.Environment,
                            replace.Files
                                .Select(file => (JoinEnvironmentPath(replace.Environment, stagingSkill, file.RelativePath), file.Content))
                                .ToArray(),
                            cancellationToken);
                        var finalSkillDirectory = JoinEnvironmentPath(replace.Environment, replace.TargetRoot, replace.SkillName);
                        await _runtime.DeleteTreeAsync(replace.Environment, finalSkillDirectory, cancellationToken);
                        await _runtime.MoveDirectoryAsync(replace.Environment, stagingSkill, finalSkillDirectory, cancellationToken);
                        break;
                    case GuideSkillRemove remove:
                        await PrepareStagingRootAsync(remove.Environment, remove.TargetRoot, null, preparedTargets, stagingRoots, cancellationToken);
                        foreach (var skillName in remove.SkillNames)
                        {
                            await _runtime.DeleteTreeAsync(
                                remove.Environment,
                                JoinEnvironmentPath(remove.Environment, remove.TargetRoot, skillName),
                                cancellationToken);
                        }
                        await RemoveTargetRootWhenEmptyAsync(remove.Environment, remove.TargetRoot, cancellationToken);
                        break;
                    case GuideLocalFileMutation local:
                        if (local.Content is null)
                        {
                            if (File.Exists(local.Path)) File.Delete(local.Path);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(local.Path)!);
                            var temporary = $"{local.Path}.{Guid.NewGuid():N}.tmp";
                            await File.WriteAllBytesAsync(temporary, local.Content, cancellationToken);
                            File.Move(temporary, local.Path, overwrite: true);
                        }
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported guide mutation: {mutation.GetType().Name}");
                }
            }

            ReportPhase(progress, "apply.verify", "Verifying installed skill trees…");
            if (plannedState is not null)
            {
                foreach (var installation in plannedState.Installations)
                {
                    var installationChecks = await InspectInstallationAsync(installation, progress, cancellationToken);
                    var catalogCheck = installationChecks.FirstOrDefault(check =>
                        check.Id == $"skills.{installation.Environment.Selector}.{TargetCheckName(installation.Target, installation.TargetRoot)}.catalog");
                    if (catalogCheck is { Status: not GuideCheckStatus.Ok })
                    {
                        throw new IOException($"Post-apply skill verification failed: {catalogCheck.Message}");
                    }
                }
            }
        }
        finally
        {
            foreach (var (stagingEnvironment, stagingRoot) in stagingRoots)
            {
                try
                {
                    // The staging tree only outlives an apply when something failed; dropping it
                    // never touches installed skills because every staged skill is moved out first.
                    await _runtime.DeleteTreeAsync(stagingEnvironment, stagingRoot, CancellationToken.None);
                }
                catch (Exception cleanupException) when (cleanupException is IOException or InvalidOperationException)
                {
                    // Best-effort cleanup; a stale staging directory never blocks the next apply.
                }
            }
        }
    }

    /// <summary>
    /// Creates the target root on first touch, removes stale staging trees from earlier failed
    /// applies, and records live staging roots for the final cleanup pass.
    /// </summary>
    private async Task PrepareStagingRootAsync(
        GuideEnvironment environment,
        string targetRoot,
        string? stagingRoot,
        HashSet<string> preparedTargets,
        List<(GuideEnvironment Environment, string Path)> stagingRoots,
        CancellationToken cancellationToken)
    {
        if (!preparedTargets.Add(targetRoot))
        {
            return;
        }
        await _runtime.CreateDirectoryAsync(environment, targetRoot, cancellationToken);
        var entries = await _runtime.ListEntriesAsync(environment, targetRoot, cancellationToken);
        foreach (var entry in entries.Where(static entry =>
                     Path.GetFileName(entry.Path.Replace('/', '\\')).StartsWith(".monica", StringComparison.Ordinal)
                     && entry.Path.Contains("staging-", StringComparison.Ordinal)))
        {
            await _runtime.DeleteTreeAsync(environment, entry.Path, cancellationToken);
        }
        if (stagingRoot is not null)
        {
            stagingRoots.Add((environment, stagingRoot));
        }
    }

    private async Task RemoveTargetRootWhenEmptyAsync(
        GuideEnvironment environment,
        string targetRoot,
        CancellationToken cancellationToken)
    {
        var entries = await _runtime.ListEntriesAsync(environment, targetRoot, cancellationToken);
        if (entries.Count == 0)
        {
            await _runtime.DeleteDirectoryAsync(environment, targetRoot, cancellationToken);
        }
    }

    /// <summary>Verifies one recorded installation by recomputing each skill's tree digest.</summary>
    private async Task<List<GuideCheck>> InspectInstallationAsync(
        GuideSkillInstallation installation,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        var targetName = TargetCheckName(installation.Target, installation.TargetRoot);
        ReportPhase(progress, $"status.skills.{installation.Environment.Selector}.{targetName}",
            $"Verifying the {targetName} skill projection in {installation.Environment.Selector} ({installation.Trees.Count} skills)…");
        var checks = new List<GuideCheck>();
        var drift = false;
        var environment = installation.Environment;

        var skillDirectories = installation.Trees
            .Select(tree => JoinEnvironmentPath(environment, installation.TargetRoot, tree.Name))
            .ToArray();
        var tree = await _runtime.ObserveTreeAsync(
            environment,
            installation.TargetRoot,
            [installation.TargetRoot, .. skillDirectories],
            [],
            skillDirectories,
            cancellationToken);

        if (tree.Paths.TryGetValue(installation.TargetRoot, out var targetObservation))
        {
            if (targetObservation.Redirected)
            {
                drift = true;
                checks.Add(Check(
                    $"skills.{environment.Selector}.{targetName}.target-redirection",
                    GuideCheckStatus.Warning,
                    "The recorded skill target root now contains a symbolic link, junction, or other reparse point.",
                    "Do not configure or unconfigure through redirected target paths; restore ordinary directories and inspect the destination."));
            }
            if (targetObservation.Kind != GuideFileSystemEntryKind.Directory)
            {
                drift = true;
                checks.Add(Check(
                    $"skills.{environment.Selector}.{targetName}.target-kind",
                    GuideCheckStatus.Warning,
                    "The recorded skill target root is missing or is not an ordinary directory.",
                    "Restore an ordinary directory, then reinstall from one immutable release."));
            }
        }
        else
        {
            drift = true;
            checks.Add(Check(
                $"skills.{environment.Selector}.{targetName}.target-kind",
                GuideCheckStatus.Warning,
                "The recorded skill target root is missing or is not an ordinary directory.",
                "Restore an ordinary directory, then reinstall from one immutable release."));
        }

        foreach (var skillTree in installation.Trees)
        {
            var skillDirectory = JoinEnvironmentPath(environment, installation.TargetRoot, skillTree.Name);
            if (tree.Paths.TryGetValue(skillDirectory, out var skillObservation) && skillObservation.Redirected)
            {
                drift = true;
                continue;
            }
            var files = tree.RootEntries.TryGetValue(skillDirectory, out var entries)
                ? entries
                    .Where(static entry => entry.Kind == GuideFileSystemEntryKind.File)
                    .Select(entry => (
                        RelativePath: RelativeSkillPath(environment, skillDirectory, entry.Path),
                        entry.Path))
                    .ToArray()
                : [];
            // A second batched observation collects the discovered file digests without one
            // host process per file.
            IReadOnlyDictionary<string, string> digests = files.Length == 0
                ? new Dictionary<string, string>(EnvironmentPathComparer(environment))
                : (await _runtime.ObserveTreeAsync(
                        environment,
                        installation.TargetRoot,
                        [],
                        files.Select(static file => file.Path).ToArray(),
                        [],
                        cancellationToken))
                    .FileDigests;
            var actualDigest = ComputeVerificationDigest(
                files.Select(file => (file.RelativePath, digests.TryGetValue(file.Path, out var content) ? content : string.Empty)));
            if (actualDigest != skillTree.TreeDigest)
            {
                drift = true;
            }
        }
        checks.Add(Check(
            $"skills.{environment.Selector}.{targetName}.catalog",
            drift ? GuideCheckStatus.Warning : GuideCheckStatus.Ok,
            drift ? "An installed skill tree drifted or disappeared." : "All installed skill trees match their recorded digests.",
            drift ? "Review local changes, then reinstall from one immutable release." : null));
        // Project directories are workspace territory: repositories may legitimately carry
        // projections the guide does not own (for example a synced checkout), so the foreign
        // scan applies to the guide-owned global roots only.
        if (installation.Target != GuideTarget.Project)
        {
            checks.AddRange(InspectForeignSkillDirectories(installation, tree));
        }

        return checks;
    }

    /// <summary>
    /// Namespaced skill directories in a shared target root that no recorded product owns:
    /// retired aliases point at their canonical replacement; anything else is leftover from an
    /// abandoned installation. Shared roots serve multiple products, so ownership is decided
    /// by the ledger, not by this product's catalog alone.
    /// </summary>
    private IEnumerable<GuideCheck> InspectForeignSkillDirectories(
        GuideSkillInstallation installation,
        GuideTreeObservation tree)
    {
        var (aliases, ownedNames) = InspectionOwnership();
        var recorded = installation.Trees.Select(static skillTree => skillTree.Name).ToHashSet(StringComparer.Ordinal);
        if (!tree.RootEntries.TryGetValue(installation.TargetRoot, out var entries))
        {
            yield break;
        }

        foreach (var directory in entries
                     .Where(static entry => entry.Kind == GuideFileSystemEntryKind.Directory)
                     .Select(static entry => EntryName(entry.Path))
                     .Where(name => name.StartsWith(_definition.SkillNamespacePrefix, StringComparison.Ordinal))
                     .Where(name => !recorded.Contains(name) && !ownedNames.Contains(name))
                     .OrderBy(static name => name, StringComparer.Ordinal))
        {
            if (aliases is not null && aliases.TryGetValue(directory, out var alias))
            {
                yield return Check(
                    $"skills.{installation.Environment.Selector}.{TargetCheckName(installation.Target, installation.TargetRoot)}.stale-alias",
                    GuideCheckStatus.Warning,
                    $"Retired skill directory '{directory}' remains in the target root.",
                    alias.Diagnostic);
            }
            else
            {
                yield return Check(
                    $"skills.{installation.Environment.Selector}.{TargetCheckName(installation.Target, installation.TargetRoot)}.foreign",
                    GuideCheckStatus.Warning,
                    $"Unmanaged skill directory '{directory}' is not part of any installed release.",
                    "Remove it manually if it belongs to an abandoned installation.");
            }
        }
    }

    private static string EntryName(string path)
        => path.Split('/', '\\').Last(static segment => segment.Length > 0);

    private (IReadOnlyDictionary<string, GuideSkillAlias>? Aliases, HashSet<string> OwnedNames) InspectionOwnership()
    {
        if (_inspectionOwnedNames is null)
        {
            try
            {
                var ledger = LoadLedger(out _);
                _inspectionOwnedNames = (ledger?.Products ?? [])
                    .SelectMany(static product => product.Installations)
                    .SelectMany(static installation => installation.Trees)
                    .Select(static skillTree => skillTree.Name)
                    .ToHashSet(StringComparer.Ordinal);
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
            {
                _inspectionOwnedNames = [];
            }

            try
            {
                var state = LoadProductState();
                _inspectionAliases = state is null
                    ? null
                    : GuideReleaseMetadata.LoadSkillCatalog(Path.Combine(state.BundleRoot, "skills")).Aliases;
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
            {
                _inspectionAliases = null;
            }
        }

        return (_inspectionAliases, _inspectionOwnedNames);
    }

    /// <summary>Advisory host detection with best-effort CLI versions; never gates configuration.</summary>
    private async Task<List<GuideCheck>> DetectHostsAsync(
        GuideEnvironment environment,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        // Each candidate probe shells out into the environment (WSL hops included), so every
        // probe reports its own phase and the probes run concurrently: the Node-based hosts
        // (claude) take seconds per version read while native CLIs return instantly, and a
        // sequential loop would always pay their sum instead of the slowest one.
        var probes = await Task.WhenAll(Enum.GetValues<GuideAgent>().Select(agent => ProbeHostAsync(
            environment, agent, progress, cancellationToken)));
        // The probes run concurrently; the enum order keeps the check output deterministic.
        return probes.Where(static check => check is not null).Cast<GuideCheck>().ToList();
    }

    private async Task<GuideCheck?> ProbeHostAsync(
        GuideEnvironment environment,
        GuideAgent agent,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        var command = agent.ToString().ToLowerInvariant();
        ReportPhase(progress, $"status.hosts.{environment.Selector}.{command}",
            $"Detecting {command} in {environment.Selector}…");
        if (!_runtime.CommandExists(environment, command))
        {
            return null;
        }
        var version = await _runtime.CommandVersionAsync(environment, command, cancellationToken);
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["environment"] = environment.Selector,
            ["version"] = version ?? string.Empty
        };
        return Check(
            $"agent.{command}.{environment.Selector}.detected",
            GuideCheckStatus.Ok,
            version is null
                ? $"{agent} detected in {environment.Selector}; its version could not be read."
                : $"{agent} {version} detected in {environment.Selector}.")
            with { Details = details };
    }

    /// <summary>Loads the catalog's file bytes once, keyed by skill name and ordered by relative path.</summary>
    private static Dictionary<string, IReadOnlyList<GuideCatalogFile>> LoadCatalogFiles(
        string skillsRoot,
        SkillCatalog catalog)
        => GuideReleaseMetadata.EnumerateCatalogFiles(skillsRoot, catalog)
            .GroupBy(static source => source.SkillName, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                group => (IReadOnlyList<GuideCatalogFile>)group
                    .Select(static source => new GuideCatalogFile(
                        source.RelativePath.Replace('\\', '/'),
                        File.ReadAllBytes(source.SourcePath)))
                    .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

    /// <summary>
    /// Resolves the catalog skills one configure installs: explicit skills, a named profile's
    /// closure, or the whole catalog. Returns null when the selection itself is invalid; the
    /// added check blocks apply.
    /// </summary>
    private static IReadOnlyList<SkillCatalogEntry>? ResolveSelectedSkills(
        SkillCatalog catalog,
        GuideConfigureRequest request,
        ICollection<GuideCheck> checks)
    {
        if (request.Skills is { Count: > 0 } && request.Profile is not null)
        {
            checks.Add(Check(
                "skills.selection",
                GuideCheckStatus.Error,
                "Explicit skills and a profile cannot be combined in one configure.",
                "Pass either --skill selections or one --profile, not both."));
            return null;
        }
        if (request.Skills is { Count: > 0 })
        {
            var byName = catalog.Skills.ToDictionary(static skill => skill.Name, StringComparer.Ordinal);
            var unknown = request.Skills.FirstOrDefault(name => !byName.ContainsKey(name));
            if (unknown is not null)
            {
                checks.Add(Check(
                    "skills.selection",
                    GuideCheckStatus.Error,
                    $"The catalog has no skill named '{unknown}'.",
                    "Choose skill names exactly as the packaged catalog lists them."));
                return null;
            }
            return request.Skills
                .Distinct(StringComparer.Ordinal)
                .Select(name => byName[name])
                .OrderBy(static skill => skill.Name, StringComparer.Ordinal)
                .ToArray();
        }
        if (request.Profile is not null)
        {
            var inProfile = catalog.Skills
                .Where(skill => skill.Profiles?.Contains(request.Profile, StringComparer.Ordinal) == true)
                .OrderBy(static skill => skill.Name, StringComparer.Ordinal)
                .ToArray();
            if (inProfile.Length == 0)
            {
                checks.Add(Check(
                    "skills.selection",
                    GuideCheckStatus.Error,
                    $"The catalog declares no skills for profile '{request.Profile}'.",
                    "Choose a profile the packaged catalog declares on its skills."));
                return null;
            }
            return inProfile;
        }
        return catalog.Skills.ToArray();
    }

    /// <summary>
    /// Skill directories are owned by exactly one product. A name another product's ledger
    /// entry already records is refused instead of silently replaced.
    /// </summary>
    private void CheckCrossProductOwnership(GuideLedger? ledger, IReadOnlyList<SkillCatalogEntry> selected, ICollection<GuideCheck> checks)
    {
        var foreign = (ledger?.Products ?? [])
            .Where(product => !string.Equals(product.ProductId, _definition.ProductId, StringComparison.Ordinal))
            .SelectMany(product => product.Installations.SelectMany(static installation => installation.Trees))
            .Select(static tree => tree.Name)
            .ToHashSet(StringComparer.Ordinal);
        var collisions = selected
            .Where(skill => foreign.Contains(skill.Name))
            .Select(static skill => skill.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (collisions.Length > 0)
        {
            checks.Add(Check(
                "skills.ownership",
                GuideCheckStatus.Error,
                $"Another guide-managed product already owns skill directories named: {string.Join(", ", collisions)}.",
                "Uninstall those projections from the other product, or keep each product's skill names disjoint."));
        }
    }

    /// <summary>One installation target selected for a configure: its kind, root, and owning workspace.</summary>
    private sealed record PlannedTarget(
        GuideEnvironment Environment,
        GuideTarget Target,
        string TargetRoot,
        string? WorkspaceRoot);

    /// <summary>
    /// Resolves what a configure installs and where. A workspace request installs the
    /// workspace profile's closure into its configured project directories on the native
    /// environment; otherwise explicit selections (global targets) or — with no selection at
    /// all — every installation recorded in the guide state (the release-update flow, which
    /// replays project installations verbatim).
    /// </summary>
    private (IReadOnlyList<SkillCatalogEntry>? Selected, List<PlannedTarget> Targets, string? WorkspaceRoot)
        ResolveInstallScope(
            GuideConfigureRequest request,
            SkillCatalog catalog,
            ProductGuideState? previous,
            ICollection<GuideCheck> checks)
    {
        if (request.Workspace is not null)
        {
            var workspaceRoot = Path.GetFullPath(request.Workspace);
            if (!Directory.Exists(workspaceRoot))
            {
                checks.Add(Check("workspace.root", GuideCheckStatus.Error,
                    $"Workspace is not a directory: {workspaceRoot}."));
                return (null, [], null);
            }

            var config = GuideWorkspaceStore.LoadConfig(workspaceRoot, out var configIssue);
            if (configIssue is not null)
            {
                checks.Add(Check("workspace.config", GuideCheckStatus.Error, configIssue,
                    "Run guide init --profile <profile> and approve the plan to rewrite it."));
                return (null, [], null);
            }

            if (config is null)
            {
                checks.Add(Check("workspace.profile", GuideCheckStatus.Error,
                    $"The workspace is not initialized: {GuideWorkspaceStore.ConfigPath(workspaceRoot)} does not exist.",
                    "Run guide init --workspace <path> --profile <profile> and approve the plan first."));
                return (null, [], null);
            }

            var profile = request.Profile ?? config.Profile;
            var selected = SelectProfileClosure(catalog, profile, checks);
            if (selected is null)
            {
                return (null, [], null);
            }

            var targetIssues = new List<string>();
            var roots = GuideWorkspaceStore.ResolveSkillTargets(workspaceRoot, config, targetIssues);
            foreach (var issue in targetIssues)
            {
                checks.Add(Check("workspace.skill-targets", GuideCheckStatus.Error, issue,
                    "Fix the skillTargets list in .monica/guide.json; entries must stay inside the workspace."));
            }

            if (roots.Count == 0)
            {
                return (null, [], null);
            }

            var environment = NativeEnvironment();
            var planned = roots
                .Select(root => new PlannedTarget(environment, GuideTarget.Project, root, workspaceRoot))
                .ToList();
            return (selected, planned, workspaceRoot);
        }

        var selectedSkills = ResolveSelectedSkills(catalog, request, checks);
        if (selectedSkills is null)
        {
            return (null, [], null);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selections = request.Selections
            .SelectMany(static selection => selection.Targets.Distinct().Select(target => (selection.Environment, Target: target)))
            .Where(item => seen.Add($"{item.Environment.Selector}:{item.Target}"))
            .OrderBy(static item => item.Environment.Selector, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => (int)item.Target)
            .Select(item => new PlannedTarget(item.Environment, item.Target, TargetRoot(item.Environment, item.Target), null))
            .ToList();
        if (selections.Count > 0)
        {
            return (selectedSkills, selections, null);
        }

        seen.Clear();
        var recorded = (previous?.Installations ?? [])
            .Where(item => seen.Add($"{item.Environment.Selector}:{NormalizeRoot(item.TargetRoot)}"))
            .OrderBy(static item => item.Environment.Selector, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => NormalizeRoot(item.TargetRoot), StringComparer.OrdinalIgnoreCase)
            .Select(static item => new PlannedTarget(item.Environment, item.Target, item.TargetRoot, item.WorkspaceRoot))
            .ToList();
        return (selectedSkills, recorded, null);
    }

    /// <summary>Selects the profile closure shared by workspace configure and inspection.</summary>
    internal static IReadOnlyList<SkillCatalogEntry>? SelectProfileClosure(
        SkillCatalog catalog,
        string profile,
        ICollection<GuideCheck> checks)
    {
        var inProfile = catalog.Skills
            .Where(skill => skill.Profiles?.Contains(profile, StringComparer.Ordinal) == true)
            .OrderBy(static skill => skill.Name, StringComparer.Ordinal)
            .ToArray();
        if (inProfile.Length == 0)
        {
            checks.Add(Check(
                "skills.selection",
                GuideCheckStatus.Error,
                $"The catalog declares no skills for profile '{profile}'.",
                "Choose a profile the packaged catalog declares on its skills."));
            return null;
        }

        return inProfile;
    }

    /// <summary>
    /// Registers an initialized workspace during a workspace configure when it is not in the
    /// registry yet, adopting workspaces initialized before the registry existed.
    /// </summary>
    private void AdoptWorkspaceRegistration(
        string workspaceRoot,
        ICollection<GuideCheck> checks,
        ICollection<GuidePlannedMutation> mutations)
    {
        var config = GuideWorkspaceStore.LoadConfig(workspaceRoot, out _);
        if (config is null)
        {
            return;
        }

        var registry = GuideWorkspaceRegistryFile.Load(_enginePaths);
        if ((registry?.Workspaces ?? []).Any(entry =>
                string.Equals(entry.Workspace, workspaceRoot, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.ProductId, _definition.ProductId, StringComparison.Ordinal)))
        {
            return;
        }

        var next = GuideWorkspaceRegistryFile.Upsert(registry, new GuideWorkspaceEntry(
            workspaceRoot,
            _definition.ProductId,
            config.Profile,
            config.Capabilities,
            config.InstructionBlockVersion));
        GuideMutations.AddLocalWrite(
            "workspace.registry.write",
            GuideWorkspaceRegistryFile.PathFor(_enginePaths),
            GuidePlanning.JsonBytes(next),
            "Record the configured workspace in the engine registry.",
            mutations);
        checks.Add(Check("workspace.registry", GuideCheckStatus.Ok,
            $"The configured workspace was adopted into the engine registry: {workspaceRoot}"));
    }

    /// <summary>The environment the guide process runs in; project installations are native-only.</summary>
    internal static GuideEnvironment NativeEnvironment()
    {
        var kind = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";
        return new GuideEnvironment(kind, kind);
    }

    private string TargetRoot(GuideEnvironment environment, GuideTarget target)
        => JoinEnvironmentPath(
            environment,
            _runtime.UserHome(environment),
            target == GuideTarget.Shared ? ".agents" : ".claude",
            "skills");

    /// <summary>
    /// Stable check-id segment for one target. Global targets keep their historical
    /// <c>shared</c>/<c>claude</c> names; project roots carry an 8-character digest of the
    /// normalized root so several workspaces never collide in one report.
    /// </summary>
    internal static string TargetCheckName(GuideTarget target, string targetRoot)
        => target switch
        {
            GuideTarget.Shared => "shared",
            GuideTarget.Claude => "claude",
            _ => $"project-{GuidePlanning.Sha256(Encoding.UTF8.GetBytes(NormalizeRoot(targetRoot)))[..8]}"
        };

    /// <summary>Normalized root without trailing separators; identity component. POSIX-shaped WSL roots are kept verbatim so a Windows host never rewrites them.</summary>
    internal static string NormalizeRoot(string targetRoot)
    {
        var trimmed = targetRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.StartsWith('/') ? trimmed : Path.GetFullPath(trimmed);
    }

    /// <summary>Installation identity comparison: same environment and same normalized root.</summary>
    private static bool SameRoot(string left, string right)
    {
        // POSIX-shaped roots (native Unix and WSL) are case-sensitive; Windows drive roots are not.
        var comparison = NormalizeRoot(left).StartsWith('/') || NormalizeRoot(right).StartsWith('/')
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        return string.Equals(NormalizeRoot(left), NormalizeRoot(right), comparison);
    }

    /// <summary>
    /// Deterministic per-skill verification digest over sorted
    /// <c>relativePath \0 sha256(content) \0</c> records; it is computable from a batched
    /// observation's file digests without transferring file bytes back from the environment.
    /// </summary>
    private static string ComputeVerificationDigest(IEnumerable<(string RelativePath, string ContentSha256)> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal))
        {
            AppendUtf8(hash, file.RelativePath);
            hash.AppendData([0]);
            AppendUtf8(hash, file.ContentSha256);
            hash.AppendData([0]);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendUtf8(IncrementalHash hash, string value)
        => hash.AppendData(Encoding.UTF8.GetBytes(value));

    private static string RelativeSkillPath(GuideEnvironment environment, string skillDirectory, string filePath)
    {
        var separator = environment.Kind == "windows" ? '\\' : '/';
        var normalizedRoot = skillDirectory.TrimEnd(separator);
        var normalizedPath = filePath.TrimEnd(separator);
        var comparison = environment.Kind == "windows" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!normalizedPath.StartsWith(normalizedRoot + separator, comparison))
        {
            throw new InvalidDataException($"Observed file escapes its skill directory: {filePath}");
        }
        // Catalog paths are posix-style; verification digests compare identical strings.
        return normalizedPath[(normalizedRoot.Length + 1)..].Replace('\\', '/');
    }

    private static void ReportPhase(IProgress<GuidePhase>? progress, string key, string message, int completed = 0, int total = 0)
        => progress?.Report(new GuidePhase(key, message, completed, total));

    private GuidePreparedPlan Prepared(string operation, IEnumerable<GuidePlannedMutation> mutations,
        IEnumerable<GuideCheck> checks, ProductGuideState? state)
        => GuideMutations.Prepare(operation, mutations, checks, state);

    /// <summary>
    /// Adds the ledger and locator writes for one product state. The ledger file is rewritten
    /// whole with the product's entry replaced; concurrent products serialize on the engine
    /// lock, so read-modify-write stays safe.
    /// </summary>
    private void AddStateWrites(GuideLedger? ledger, ProductGuideState state, ICollection<GuidePlannedMutation> mutations)
    {
        var products = (ledger?.Products ?? [])
            .Where(product => !string.Equals(product.ProductId, state.ProductId, StringComparison.Ordinal))
            .ToList();
        products.Add(state);
        var nextLedger = new GuideLedger(
            GuideLedger.CurrentSchemaVersion,
            products.OrderBy(static product => product.ProductId, StringComparer.Ordinal).ToArray());
        AddLocalWrite("configuration.state.write", _enginePaths.GuideLedgerFile, GuidePlanning.JsonBytes(nextLedger), "Record installed skill ownership.", mutations);
        AddLocalWrite("installation.locator.write", _productPaths.InstallationLocatorFile,
            GuidePlanning.JsonBytes(new GuideInstallationLocator(GuideInstallationLocator.CurrentSchemaVersion, state.ProductId, state.ExecutablePath,
                state.BundleRoot, state.ProductVersion, state.ReleaseManifestDigest)), "Write stable bootstrap locator.", mutations);
    }

    /// <summary>
    /// Adds the ledger rewrite with this product removed; an empty ledger file is deleted so a
    /// clean machine leaves no empty state behind.
    /// </summary>
    private void AddLocalDelete(string id, string description, ICollection<GuidePlannedMutation> mutations, GuideLedger? ledger)
    {
        var remaining = (ledger?.Products ?? [])
            .Where(product => !string.Equals(product.ProductId, _definition.ProductId, StringComparison.Ordinal))
            .ToArray();
        if (remaining.Length == 0)
        {
            AddLocalDelete("configuration.state.remove", _enginePaths.GuideLedgerFile, GuidedLedgerRemovalDescription, mutations);
        }
        else
        {
            AddLocalWrite("configuration.state.write", _enginePaths.GuideLedgerFile,
                GuidePlanning.JsonBytes(new GuideLedger(GuideLedger.CurrentSchemaVersion, remaining)), GuidedLedgerRemovalDescription, mutations);
        }
    }

    private static void AddLocalWrite(string id, string path, byte[] content, string description, ICollection<GuidePlannedMutation> mutations)
        => GuideMutations.AddLocalWrite(id, path, content, description, mutations);

    private static void AddLocalDelete(string id, string path, string description, ICollection<GuidePlannedMutation> mutations)
        => GuideMutations.AddLocalDelete(id, path, description, mutations);

    private ProductGuideState? LoadProductState() => LoadProductState(out _);

    /// <summary>
    /// Loads this product's slice of the unified ownership ledger. A ledger written by an older
    /// guide schema is ignored with an explanation rather than treated as corruption: the
    /// wholesale directory model re-derives ownership from the catalog on the next configure.
    /// </summary>
    private ProductGuideState? LoadProductState(out string? incompatibility)
    {
        var ledger = LoadLedger(out incompatibility);
        return FindProductState(ledger);
    }

    private GuideLedger? LoadLedger(out string? incompatibility)
    {
        incompatibility = null;
        if (!File.Exists(_enginePaths.GuideLedgerFile)) return null;
        var ledger = JsonSerializer.Deserialize<GuideLedger>(File.ReadAllBytes(_enginePaths.GuideLedgerFile), GuidePlanning.JsonOptions)
                     ?? throw new InvalidDataException("The guide ledger is empty.");
        if (ledger.SchemaVersion != GuideLedger.CurrentSchemaVersion)
        {
            incompatibility = $"Guide ledger schema {ledger.SchemaVersion} predates this version; the recorded ledger was ignored.";
            return null;
        }
        return ledger;
    }

    private ProductGuideState? FindProductState(GuideLedger? ledger)
        => ledger?.Products.FirstOrDefault(product =>
            string.Equals(product.ProductId, _definition.ProductId, StringComparison.Ordinal));

    private async Task<GuideReport> CompleteAsync(
        GuidePreparedPlan prepared,
        string? expectedDigest,
        bool apply,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        var checks = prepared.PublicPlan.Checks.ToList();
        if (!apply) return Report(prepared.PublicPlan.Operation, checks, "Preview generated; no changes were made.",
            ["Review the plan and repeat with --apply --plan-digest."], prepared.PublicPlan);
        if (string.IsNullOrWhiteSpace(expectedDigest) || expectedDigest != prepared.PublicPlan.PlanDigest)
        {
            checks.Add(Check("plan.digest", GuideCheckStatus.Error, "Approved plan digest is stale or missing.", "Preview and approve current state."));
            return Report(prepared.PublicPlan.Operation, checks, "No changes applied.", ["Preview again."], prepared.PublicPlan);
        }
        if (checks.Any(static item => item.Status == GuideCheckStatus.Error))
            return Report(prepared.PublicPlan.Operation, checks, "Blockers prevented apply.", ["Resolve errors and preview again."], prepared.PublicPlan);
        if (prepared.PublicPlan.IsNoOp)
        {
            checks.Add(Check("plan.applied", GuideCheckStatus.Ok, "The approved plan was already satisfied; no changes were needed."));
            return Report(prepared.PublicPlan.Operation, checks, "Nothing to apply.", [], prepared.PublicPlan with { Applied = true });
        }

        await ApplyAsync(prepared.Mutations, prepared.NextState, progress, cancellationToken);
        checks.Add(Check("plan.applied", GuideCheckStatus.Ok, "The approved plan was applied and verified."));
        return Report(prepared.PublicPlan.Operation, checks, "Approved plan applied.", ["Run guide doctor."], prepared.PublicPlan with { Applied = true });
    }

    private async ValueTask<IAsyncDisposable> AcquireLockAsync(CancellationToken cancellationToken)
        => await GuideMutations.AcquireLockAsync(_enginePaths, cancellationToken);

    private async Task ProbeGetAsync(string id, Uri endpoint, ICollection<GuideCheck> checks, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(AsDirectLoopback(endpoint), cancellationToken);
            checks.Add(Check(id, response.IsSuccessStatusCode ? GuideCheckStatus.Ok : GuideCheckStatus.Error,
                $"{endpoint.AbsolutePath} returned HTTP {(int)response.StatusCode}.", response.IsSuccessStatusCode ? null : "Start the service and inspect product logs."));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        { checks.Add(Check(id, GuideCheckStatus.Error, exception.Message, $"Start {Product} on the selected loopback port.")); }
    }

    /// <summary>
    /// Framework-dependent releases resolve the ASP.NET Core shared framework at process
    /// start; the default roll-forward policy needs the exact required major.minor on disk.
    /// </summary>
    private static GuideCheck ObserveAspNetRuntime(string requiredRuntime)
    {
        // Each host keeps its shared frameworks under different install roots; DOTNET_ROOT
        // always wins because package managers and manual installs export it.
        var roots = OperatingSystem.IsWindows()
            ? new[]
            {
                Environment.GetEnvironmentVariable("DOTNET_ROOT"),
                Environment.GetEnvironmentVariable("DOTNET_ROOT(x86)"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet"),
            }
            : OperatingSystem.IsMacOS()
                ? new[]
                {
                    Environment.GetEnvironmentVariable("DOTNET_ROOT"),
                    "/usr/local/share/dotnet",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet"),
                }
                : new[]
                {
                    Environment.GetEnvironmentVariable("DOTNET_ROOT"),
                    "/usr/share/dotnet",
                    "/usr/lib/dotnet",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet"),
                };
        var sharedRootName = Path.Combine("shared", "Microsoft.AspNetCore.App");
        var installed = new SortedSet<Version>();
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }
            var sharedRoot = Path.Combine(root, sharedRootName);
            if (!Directory.Exists(sharedRoot))
            {
                continue;
            }
            foreach (var directory in Directory.EnumerateDirectories(sharedRoot))
            {
                if (Version.TryParse(Path.GetFileName(directory), out var version))
                {
                    installed.Add(version);
                }
            }
        }
        var required = Version.Parse(requiredRuntime);
        var matching = installed.Where(version => version.Major == required.Major && version.Minor == required.Minor).Max();
        if (matching is not null)
        {
            return Check(
                "runtime.aspnet",
                GuideCheckStatus.Ok,
                $"ASP.NET Core shared runtime {matching} satisfies the required {requiredRuntime}.");
        }
        var available = installed.Count > 0 ? string.Join(", ", installed.Select(static version => version.ToString())) : "none";
        var remediation = OperatingSystem.IsWindows()
            ? $"Install the runtime with 'winget install Microsoft.DotNet.AspNetCore.{required.Major}' and retry."
            : $"Install the .NET {required.Major} SDK or ASP.NET Core runtime (https://dotnet.microsoft.com/download) and retry.";
        return Check(
            "runtime.aspnet",
            GuideCheckStatus.Error,
            $"This release needs the .NET {requiredRuntime} ASP.NET Core shared runtime; installed versions: {available}.",
            remediation);
    }

    private async Task ProbeHealthAsync(
        Uri baseAddress,
        ReleaseObservation expectedRelease,
        ICollection<GuideCheck> checks,
        CancellationToken cancellationToken)
    {
        if (!_definition.DefaultRoutes.TryGetValue("health", out var healthPath))
        {
            return;
        }
        var endpoint = Combine(baseAddress, healthPath);
        try
        {
            using var response = await _http.GetAsync(AsDirectLoopback(endpoint), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                checks.Add(Check(
                    "runtime.healthz",
                    GuideCheckStatus.Error,
                    $"{endpoint.AbsolutePath} returned HTTP {(int)response.StatusCode}.",
                    $"Start {Product} and inspect its bounded rolling log."));
                return;
            }

            var health = await response.Content.ReadFromJsonAsync<GuidePublicHealth>(
                             GuidePlanning.JsonOptions,
                             cancellationToken)
                         ?? throw new InvalidDataException("The health payload is empty.");
            var expectedSchemaDigest = expectedRelease.Manifest?.McpToolSchemaDigest ?? "development";
            var expectedUi = _definition.DefaultRoutes.TryGetValue("ui", out var uiPath)
                ? Combine(baseAddress, uiPath)
                : null;
            var expectedMcp = _definition.McpPath is not null
                ? Combine(baseAddress, _definition.McpPath)
                : null;
            var expectedVersion = expectedRelease.Manifest?.ProductVersion ?? CurrentVersion;
            var exact = health.ProductVersion == expectedVersion
                        && health.DistributionKind == expectedRelease.DistributionKind
                        && health.ReleaseManifestDigest == expectedRelease.ManifestDigest
                        && health.UiUrl == expectedUi
                        && health.McpUrl == expectedMcp
                        && health.HealthStatus == "ready"
                        && health.McpToolSchemaDigest == expectedSchemaDigest;
            checks.Add(Check(
                "runtime.healthz",
                exact ? GuideCheckStatus.Ok : GuideCheckStatus.Error,
                exact
                    ? $"The loopback health endpoint matches this exact {Product} release."
                    : $"The listener returned a health payload that does not match this {Product} release, route set, or schema digest.",
                exact
                    ? null
                    : $"Stop the process on this port and start {_definition.CurrentProgramEntryPoint ?? "the product executable"} from the configured immutable release."));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
        {
            checks.Add(Check(
                "runtime.healthz",
                GuideCheckStatus.Error,
                exception.Message,
                $"Start the exact configured {Product} release on the selected loopback port."));
        }
    }

    private async Task ProbeMcpAsync(
        Uri endpoint,
        ReleaseObservation expectedRelease,
        ICollection<GuideCheck> checks,
        CancellationToken cancellationToken)
    {
        var readOnlyTool = _definition.DoctorReadOnlyTool
                           ?? expectedRelease.Manifest?.DoctorReadOnlyTool;
        if (readOnlyTool is null)
        {
            return;
        }
        var expectedVersion = expectedRelease.Manifest?.ProductVersion ?? CurrentVersion;
        try
        {
            var initialize = await SendMcpCallAsync(
                endpoint,
                1,
                "initialize",
                new
                {
                    protocolVersion = "2025-06-18",
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "monica-guide",
                        version = CurrentVersion
                    }
                },
                null,
                cancellationToken);
            var initializeResult = RequiredObject(initialize.Payload, "result");
            var serverInfo = RequiredObject(initializeResult, "serverInfo");
            var serverName = RequiredString(serverInfo, "name");
            var serverVersion = RequiredString(serverInfo, "version");
            var protocolVersion = RequiredString(initializeResult, "protocolVersion");
            if (serverName != _definition.McpServerName
                || GuideReleaseMetadata.SemVerCore(serverVersion) != GuideReleaseMetadata.SemVerCore(expectedVersion))
            {
                throw new InvalidDataException(
                    $"MCP server identity mismatch: observed {serverName} {serverVersion}; expected {_definition.McpServerName} {expectedVersion}.");
            }

            checks.Add(Check(
                "runtime.mcp-initialize",
                GuideCheckStatus.Ok,
                $"MCP initialized {serverName} {serverVersion} using protocol {protocolVersion}."));
            await SendMcpNotificationAsync(
                endpoint,
                "notifications/initialized",
                new { },
                initialize.SessionId,
                cancellationToken);

            var tools = new List<JsonElement>();
            string? cursor = null;
            var requestId = 2;
            do
            {
                var page = await SendMcpCallAsync(
                    endpoint,
                    requestId++,
                    "tools/list",
                    cursor is null ? new { } : new { cursor },
                    initialize.SessionId,
                    cancellationToken);
                var pageResult = RequiredObject(page.Payload, "result");
                if (!pageResult.TryGetProperty("tools", out var pageTools)
                    || pageTools.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException("MCP tools/list returned no tools array.");
                }
                tools.AddRange(pageTools.EnumerateArray().Select(static tool => tool.Clone()));
                cursor = pageResult.TryGetProperty("nextCursor", out var nextCursor)
                         && nextCursor.ValueKind == JsonValueKind.String
                    ? nextCursor.GetString()
                    : null;
            } while (!string.IsNullOrEmpty(cursor));

            if (tools.Count == 0)
            {
                throw new InvalidDataException("MCP tools/list returned an empty tool catalog.");
            }
            var names = tools.Select(tool => RequiredString(tool, "name")).ToHashSet(StringComparer.Ordinal);
            if (!names.Contains(readOnlyTool))
            {
                throw new InvalidDataException($"MCP tool catalog lacks the expected read-only {readOnlyTool} operation.");
            }

            var liveSchemaDigest = ComputeMcpToolSchemaDigest(tools);
            var expectedSchemaDigest = expectedRelease.Manifest?.McpToolSchemaDigest;
            if (expectedSchemaDigest is not null && liveSchemaDigest != expectedSchemaDigest)
            {
                throw new InvalidDataException(
                    $"MCP tool schema digest mismatch: observed {liveSchemaDigest}; expected {expectedSchemaDigest}.");
            }
            checks.Add(Check(
                "runtime.mcp-tools",
                GuideCheckStatus.Ok,
                $"MCP exposed {tools.Count} exact tools with schema digest {liveSchemaDigest}."));

            var readOnlyCall = await SendMcpCallAsync(
                endpoint,
                requestId,
                "tools/call",
                new { name = readOnlyTool, arguments = new { } },
                initialize.SessionId,
                cancellationToken);
            var callResult = RequiredObject(readOnlyCall.Payload, "result");
            if (callResult.TryGetProperty("isError", out var isError)
                && isError.ValueKind == JsonValueKind.True)
            {
                throw new InvalidDataException($"The read-only MCP onboarding probe returned isError=true.");
            }
            checks.Add(Check(
                "runtime.mcp-readonly",
                GuideCheckStatus.Ok,
                "The read-only machine API onboarding probe completed successfully."));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
        {
            checks.Add(Check(
                "runtime.mcp",
                GuideCheckStatus.Error,
                exception.Message,
                $"Use the exact loopback {_definition.McpPath} endpoint from the configured release and re-export the operations skill after repair."));
        }
    }

    private async Task<McpCallResponse> SendMcpCallAsync(
        Uri endpoint,
        int id,
        string method,
        object parameters,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        using var request = CreateMcpRequest(
            endpoint,
            new { jsonrpc = "2.0", id, method, @params = parameters },
            sessionId);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidDataException(
                $"MCP {method} returned HTTP {(int)response.StatusCode}: {Truncate(errorBody, 400)}");
        }

        var payload = await ReadMcpPayloadAsync(response, cancellationToken);
        if (!payload.TryGetProperty("id", out var responseId)
            || responseId.ValueKind != JsonValueKind.Number
            || responseId.GetInt32() != id)
        {
            throw new InvalidDataException($"MCP {method} returned an unexpected JSON-RPC id.");
        }
        if (payload.TryGetProperty("error", out var error)
            && error.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            throw new InvalidDataException($"MCP {method} failed: {Truncate(error.GetRawText(), 400)}");
        }

        var observedSession = response.Headers.TryGetValues("Mcp-Session-Id", out var values)
            ? values.FirstOrDefault()
            : sessionId;
        return new McpCallResponse(payload, observedSession);
    }

    private async Task SendMcpNotificationAsync(
        Uri endpoint,
        string method,
        object parameters,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        using var request = CreateMcpRequest(
            endpoint,
            new { jsonrpc = "2.0", method, @params = parameters },
            sessionId);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidDataException(
                $"MCP {method} returned HTTP {(int)response.StatusCode}: {Truncate(errorBody, 400)}");
        }
    }

    private static HttpRequestMessage CreateMcpRequest(
        Uri endpoint,
        object payload,
        string? sessionId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Accept.ParseAdd("application/json, text/event-stream");
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
        }
        return request;
    }

    private static async Task<JsonElement> ReadMcpPayloadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.Equals(
                response.Content.Headers.ContentType?.MediaType,
                "text/event-stream",
                StringComparison.OrdinalIgnoreCase))
        {
            text = text.Replace("\r", string.Empty, StringComparison.Ordinal)
                .Split('\n')
                .Where(static line => line.StartsWith("data:", StringComparison.Ordinal))
                .Select(static line => line["data:".Length..].Trim())
                .LastOrDefault(static line => line.Length > 0)
               ?? throw new InvalidDataException("MCP event-stream response contains no data event.");
        }
        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("MCP JSON-RPC response must be an object.");
        }
        return document.RootElement.Clone();
    }

    private static JsonElement RequiredObject(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"MCP response is missing object '{name}'.");
        }
        return value;
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"MCP response is missing string '{name}'.");
        }
        return value.GetString()!;
    }

    /// <summary>
    /// Canonical SHA-256 digest over the whitelisted tool schema properties. This mirrors the
    /// framework implementation in Monica.AI (<c>McpToolSchemaDigest</c>) byte-for-byte so guide
    /// probes and generated operations skills share one identity.
    /// </summary>
    internal static string ComputeMcpToolSchemaDigest(IReadOnlyList<JsonElement> tools)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartArray();
            foreach (var tool in tools.OrderBy(static tool => RequiredString(tool, "name"), StringComparer.Ordinal))
            {
                var name = RequiredString(tool, "name");
                if (!names.Add(name))
                {
                    throw new InvalidDataException($"MCP tools/list returned duplicate tool '{name}'.");
                }

                writer.WriteStartObject();
                foreach (var propertyName in new[]
                         {
                             "annotations", "description", "execution", "inputSchema", "name", "outputSchema", "title"
                         })
                {
                    if (!tool.TryGetProperty(propertyName, out var property)
                        || property.ValueKind == JsonValueKind.Undefined)
                    {
                        continue;
                    }
                    writer.WritePropertyName(propertyName);
                    WriteCanonicalJson(writer, property);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        return $"sha256:{Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant()}";
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(static property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidDataException($"Unsupported MCP JSON kind {value.ValueKind}.");
        }
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private ReleaseObservation ObserveSelfRelease(string expectedVersion, string? directory = null)
        => GuideReleaseMetadata.Observe(
            directory ?? _applicationDirectory,
            expectedVersion,
            _definition,
            expectedVersion == CurrentVersion ? _currentAssemblyVersion : null);

    private GuideReport Report(string command, IReadOnlyList<GuideCheck> checks, string message,
        IReadOnlyList<string> nextActions, GuidePlan? plan = null)
        => new(GuideContractVersions.CURRENT, CurrentVersion, StatusOf(checks), checks,
            Summary(command, message, nextActions), plan);

    private static GuideSummary Summary(string command, string message, IReadOnlyList<string> nextActions)
        => new(command, message, DateTimeOffset.UtcNow, nextActions);

    private static GuideStatus StatusOf(IEnumerable<GuideCheck> checks)
    {
        var statuses = checks.Select(static item => item.Status).ToArray();
        return statuses.Contains(GuideCheckStatus.Error) ? GuideStatus.Error
            : statuses.Contains(GuideCheckStatus.Warning) ? GuideStatus.Warning : GuideStatus.Ready;
    }

    private static GuideCheck Check(string id, GuideCheckStatus status, string message, string? remediation = null)
        => new(id, status, message, remediation);

    private static Uri NormalizeLoopback(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp || (!(IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip))
            && !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"{nameof(AgentGuideService)} requires a loopback HTTP address.", nameof(uri));
        return new Uri(uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/");
    }

    private static Uri AsDirectLoopback(Uri uri)
    {
        // Probing a literal loopback IP skips per-request 'localhost' name resolution,
        // which can cost seconds per failed probe on machines with unusual DNS or IPv6 setups.
        // Identity comparisons keep using the original address.
        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return new UriBuilder(uri) { Host = "127.0.0.1" }.Uri;
        }

        return uri;
    }

    private static string ResolveBundleRoot(string executable, string? manifestPath)
    {
        if (!string.IsNullOrWhiteSpace(manifestPath)) return Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var app = Path.GetDirectoryName(executable)!;
        return string.Equals(Path.GetFileName(app), "app", StringComparison.OrdinalIgnoreCase) ? Directory.GetParent(app)?.FullName ?? app : app;
    }

    private string ResolveExecutable(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return Path.GetFullPath(requested);
        }

        // Entry file names are platform-specific (".exe" on Windows, bare on Unix); probing
        // every published name resolves the bundle's own entry regardless of the host.
        foreach (var executableName in _definition.Platforms.Select(static platform => platform.ExecutableName))
        {
            var packaged = Path.Combine(_applicationDirectory, executableName);
            if (File.Exists(packaged))
            {
                return packaged;
            }
        }

        return Path.GetFullPath(
            Environment.ProcessPath
            ?? throw new InvalidOperationException("The current executable path is unavailable."));
    }

    private static Uri Combine(Uri baseAddress, string absolutePath)
        => new(baseAddress, absolutePath.TrimStart('/'));

    private static IReadOnlyList<GuideSkillInstallation> FilterInstallations(IReadOnlyList<GuideSkillInstallation> values, IReadOnlyList<GuideEnvironment>? environments)
        => environments is not { Count: > 0 } ? values : values.Where(value => environments.Any(env => EqEnv(env, value.Environment))).ToArray();

    private static bool EqEnv(GuideEnvironment left, GuideEnvironment right)
        => string.Equals(left.Selector, right.Selector, StringComparison.OrdinalIgnoreCase);

    private static string JoinEnvironmentPath(GuideEnvironment environment, params string[] parts)
        => environment.Kind == "windows"
            ? Path.Combine(parts.Select(static part => part.Replace('/', '\\')).ToArray())
            : string.Join('/', parts.Select((part, index) => index == 0 ? part.TrimEnd('/') : part.Trim('/')));

    private static StringComparer EnvironmentPathComparer(GuideEnvironment environment)
        => environment.Kind == "windows" ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static async Task<bool> IsPortListeningAsync(int port, CancellationToken cancellationToken)
    {
        foreach (var address in new[] { IPAddress.Loopback, IPAddress.IPv6Loopback })
        {
            using var client = new TcpClient(address.AddressFamily);
            try
            {
                await client.ConnectAsync(address, port, cancellationToken)
                    .AsTask()
                    .WaitAsync(TimeSpan.FromMilliseconds(500), cancellationToken);
                return true;
            }
            catch (Exception exception) when (exception is SocketException or TimeoutException)
            {
            }
        }
        return false;
    }

    private sealed record McpCallResponse(JsonElement Payload, string? SessionId);
}

/// <summary>Version metadata of the guide engine assembly.</summary>
public static class AgentGuideInfo
{
    private static readonly Lazy<string> CURRENT_VERSION = new(ResolveCurrentVersion);

    /// <summary>The build-supplied assembly informational version.</summary>
    public static string CurrentVersion() => CURRENT_VERSION.Value;

    private static string ResolveCurrentVersion()
    {
        var assembly = typeof(AgentGuideInfo).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }
}
