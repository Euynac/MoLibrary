using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Monica.Guide;
using Monica.Guide.Setup;

namespace Monica.Guide.App;

/// <summary>Aggregated dashboard projection: installation, runtime, and guide health.</summary>
public sealed record SetupDashboardView(
    string? InstalledVersion,
    string? BundleRoot,
    bool CockpitRunning,
    int ConfiguredPort,
    SetupChecksView Checks,
    string SummaryMessage,
    IReadOnlyList<SetupAgentPresence> Presence,
    string? Error);

/// <summary>
/// The structural dashboard facts available in milliseconds: recorded installation
/// identity and loopback reachability. The overview paints these instantly while the
/// full diagnosis (health checks, detected agent hosts) completes in the background.
/// </summary>
public sealed record SetupDashboardShellView(
    string? InstalledVersion,
    string? BundleRoot,
    bool CockpitRunning,
    int ConfiguredPort);

/// <summary>Result of one preview or apply operation.</summary>
public sealed record SetupOperationView(
    bool Applied,
    GuideStatus Status,
    SetupChecksView Checks,
    SetupPlanView Plan,
    string? PlanDigest,
    string? Error);

/// <summary>Latest-release feed state compared against the installed product.</summary>
public sealed record SetupUpdateFeedView(
    bool UpdateAvailable,
    string? InstalledVersion,
    string? LatestVersion,
    string? LatestTag,
    string? LatestTitle,
    string? LatestNotes,
    DateTimeOffset? LatestPublishedAt,
    long ArchiveSizeBytes,
    string? Error);

/// <summary>A downloaded and validated release extracted beside the current installation.</summary>
public sealed record SetupStagedUpdateView(SetupBundleView Bundle, string? Error);

/// <summary>One installation recorded in the guide state, removable on its own.</summary>
public sealed record SetupInstallationView(
    string EnvironmentSelector,
    GuideTarget Target,
    string TargetRoot,
    int SkillCount,
    string? Workspace = null);

/// <summary>One registered workspace projected for the workspaces page.</summary>
public sealed record SetupWorkspaceView(
    string Workspace,
    string Profile,
    IReadOnlyList<string> Capabilities,
    bool DirectoryExists,
    bool ConfigurationCurrent,
    int InstalledSkillCount,
    int ProfileSkillCount,
    bool InstructionsCurrent,
    IReadOnlyList<string> Issues);

/// <summary>Advisory detection of one workspace candidate for the add-workspace flow.</summary>
public sealed record SetupWorkspaceDetectionView(
    GuideWorkspaceDetectionOutcome Outcome,
    string? CandidateProfile,
    string Confidence,
    string Reason,
    IReadOnlyList<string> Capabilities,
    string? FrameworkVersion,
    bool Initialized,
    string Error);

/// <summary>Localized phase labels map onto these stable phase keys.</summary>
public sealed record SetupProgressView(string Phase, long Completed, long Total);

/// <summary>Current convenience shell integration for the installed executable.</summary>
public sealed record SetupDesktopIntegrationView(
    bool Installed,
    bool DesktopShortcut,
    bool StartMenuShortcut,
    bool AutoStart,
    string? Error);

/// <summary>Thin orchestration over the guide engine for the setup UI of the session product.</summary>
public sealed class SetupFacade(SetupSession session)
{
    private static readonly JsonSerializerOptions LocatorJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _dashboardGate = new();

    private Task<SetupDashboardView>? _dashboardLoad;

    private int _dashboardSequence;

    private readonly object _dashboardPhaseLogLock = new();

    private readonly Queue<GuidePhase> _recentDashboardPhases = new();

    /// <summary>
    /// Live phases of the running dashboard diagnosis, raised to every subscriber no matter which
    /// caller started the load — the startup warmup owns the first load, yet the Overview page
    /// must still show the diagnosis steps.
    /// </summary>
    public event Action<GuidePhase>? DashboardPhaseReported;

    /// <summary>The diagnosis phases recorded so far this load, so a late Overview subscriber catches up.</summary>
    public IReadOnlyList<GuidePhase> RecentDashboardPhases
    {
        get
        {
            lock (_dashboardPhaseLogLock)
            {
                return [.. _recentDashboardPhases];
            }
        }
    }

    private IProgress<GuidePhase> BroadcastDashboardProgress(IProgress<GuidePhase>? caller)
        => new BroadcastProgress(caller, this);

    private sealed class BroadcastProgress(IProgress<GuidePhase>? caller, SetupFacade owner) : IProgress<GuidePhase>
    {
        public void Report(GuidePhase phase)
        {
            lock (owner._dashboardPhaseLogLock)
            {
                owner._recentDashboardPhases.Enqueue(phase);
                while (owner._recentDashboardPhases.Count > 64)
                {
                    owner._recentDashboardPhases.Dequeue();
                }
            }
            caller?.Report(phase);
            owner.DashboardPhaseReported?.Invoke(phase);
        }
    }

    private AgentProductDefinition Product => session.CurrentProduct;

    /// <summary>Path of the installed executable, or null when nothing is configured.</summary>
    public string? InstalledExecutablePath => ReadLocator()?.ExecutablePath;

    private AgentGuideService CreateService(string? applicationDirectory = null)
        => new(
            Product,
            applicationDirectory: applicationDirectory,
            currentVersion: () => GuideAppInfo.CurrentVersion);

    /// <summary>
    /// Starts the one background diagnosis of this wizard run. Host detection shells out to
    /// agent CLIs and takes seconds, so it must never sit in front of the first Overview
    /// paint; every page load joins the started task or reads the session cache instead.
    /// </summary>
    public void StartDashboardWarmup() => _ = WarmDashboardAsync();

    private async Task WarmDashboardAsync()
    {
        try
        {
            await LoadDashboardAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A warmup failure must not crash the process; the next page load surfaces it.
            session.DashboardCache = new SetupDashboardView(
                null, null, false, CurrentPort(),
                new SetupChecksView(GuideStatus.Error, []),
                exception.Message, [], exception.Message);
        }
    }

    /// <summary>
    /// Loads installation state, runtime health, and detected agent hosts. The result is
    /// cached for the whole wizard run; <paramref name="force"/> reruns the diagnosis for
    /// an explicit refresh while concurrent callers always join the in-flight load.
    /// </summary>
    public Task<SetupDashboardView> LoadDashboardAsync(
        bool force = false,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
    {
        lock (_dashboardGate)
        {
            if (!force && session.DashboardCache is { } cached)
            {
                return Task.FromResult(cached);
            }

            if (!force && _dashboardLoad is not null)
            {
                return _dashboardLoad;
            }

            // A forced refresh supersedes an older in-flight load; the sequence keeps the
            // slower older result from overwriting the newer one in the session cache.
            var sequence = ++_dashboardSequence;
            lock (_dashboardPhaseLogLock)
            {
                _recentDashboardPhases.Clear();
            }
            _dashboardLoad = RunDashboardLoadAsync(sequence, BroadcastDashboardProgress(progress), cancellationToken);
            return _dashboardLoad;
        }
    }

    private async Task<SetupDashboardView> RunDashboardLoadAsync(
        int sequence,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            // The guide engine still has synchronous prefixes (bundle hashing, WSL discovery),
            // so the whole diagnosis runs off any Blazor render thread.
            var view = await Task.Run(
                () => LoadDashboardUncachedAsync(progress, cancellationToken),
                cancellationToken);
            lock (_dashboardGate)
            {
                if (sequence == _dashboardSequence)
                {
                    session.DashboardCache = view;
                    session.PresenceCache = view.Presence;
                    session.CachedPort = view.ConfiguredPort;
                }
            }

            return view;
        }
        finally
        {
            lock (_dashboardGate)
            {
                if (_dashboardLoad is not null && sequence == _dashboardSequence)
                {
                    _dashboardLoad = null;
                }
            }
        }
    }

    private async Task<SetupDashboardView> LoadDashboardUncachedAsync(
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            // DiagnoseAsync internally re-runs the status inspection and appends the
            // runtime probes, so one call yields both check lists without duplicate work.
            using var service = CreateService();
            var health = await service.DiagnoseAsync(
                new GuideInspectRequest(),
                progress: progress,
                cancellationToken: cancellationToken);
            var locator = ReadLocator();
            var port = CurrentPort();
            return new SetupDashboardView(
                locator?.ProductVersion,
                locator?.BundleRoot,
                Product.ServesLoopback && await IsPortListeningAsync(port),
                port,
                GuideSetupPresenter.PresentChecks(health.Checks, health.Status),
                health.Summary.Message,
                GuideSetupPresenter.DeriveAgentPresence(
                    new GuideReport(health.SchemaVersion, health.ProductVersion, health.Status, health.Checks, health.Summary, health.Plan)),
                null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new SetupDashboardView(
                null, null, false, CurrentPort(),
                new SetupChecksView(GuideStatus.Error, []),
                exception.Message, [], exception.Message);
        }
    }

    private int CurrentPort()
        => Product.ServesLoopback
            ? GuideProductConfiguration.Load(Product).Port
            : 0;

    /// <summary>
    /// Reads the fast structural dashboard facts: the recorded locator and one loopback
    /// reachability probe. No subprocesses, no bundle hashing, no host detection.
    /// </summary>
    public async Task<SetupDashboardShellView> LoadDashboardShellAsync(CancellationToken cancellationToken = default)
    {
        var (locator, port) = await Task.Run(() => (ReadLocator(), CurrentPort()), cancellationToken);
        var running = Product.ServesLoopback && await IsPortListeningAsync(port);
        return new SetupDashboardShellView(locator?.ProductVersion, locator?.BundleRoot, running, port);
    }

    /// <summary>Validates one extracted bundle candidate directory for the session product.</summary>
    public SetupBundleView ValidateBundle(string path)
    {
        var view = GuideSetupPresenter.PresentBundle(path);
        if (view.ProductId is { } productId
            && KnownAgentProducts.FindByProductId(productId) is { } product
            && !ReferenceEquals(product, Product))
        {
            // The chosen bundle belongs to another registered product; the whole wizard
            // follows it so port, update, and integration surfaces stay coherent.
            session.SwitchProduct(product);
        }
        return view;
    }

    /// <summary>
    /// Resolves the default release bundle by structure: the bundle that ships this guide
    /// executable, falling back to the recorded installation locator. Null when neither applies.
    /// </summary>
    public string? DetectDefaultBundle()
    {
        var structural = GuideSetupPresenter.DetectBundleRootByStructure(AppContext.BaseDirectory);
        if (structural is not null)
        {
            return structural;
        }
        return ReadLocator()?.BundleRoot;
    }

    /// <summary>Validates one bundle off the render thread, hashing it with progress.</summary>
    public Task<SetupBundleView> ValidateBundleAsync(
        string path,
        CancellationToken cancellationToken = default)
        => Task.Run(() => ValidateBundle(path), cancellationToken);

    /// <summary>Previews the configure plan for the selected bundle, targets, and port.</summary>
    public async Task<SetupOperationView> PreviewInstallAsync(
        string bundleApplicationDirectory,
        IReadOnlyList<GuideTargetSelection> selections,
        int? port,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GuideConfigureRequest(
                null,
                port is null ? null : new Uri($"http://localhost:{port}"),
                selections);
            using var service = CreateService(bundleApplicationDirectory);
            var report = await Task.Run(
                () => service.PreviewConfigureAsync(request, progress, cancellationToken),
                cancellationToken);
            return ToOperationView(report);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ErrorView(exception.Message);
        }
    }

    /// <summary>Applies a digest-locked install or upgrade plan.</summary>
    public async Task<SetupOperationView> ApplyInstallAsync(
        string bundleApplicationDirectory,
        IReadOnlyList<GuideTargetSelection> selections,
        int? port,
        string planDigest,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var previous = ReadLocator();
            var request = new GuideConfigureRequest(
                null,
                port is null ? null : new Uri($"http://localhost:{port}"),
                selections);
            using var service = CreateService(bundleApplicationDirectory);
            var report = await Task.Run(
                () => service.ApplyConfigureAsync(request, planDigest, progress, cancellationToken),
                cancellationToken);
            if (report.Plan?.Applied == true)
            {
                RepointDesktopIntegrationAfterInstall(previous, ReadLocator());
            }
            return ToOperationView(report);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ErrorView(exception.Message);
        }
    }

    /// <summary>
    /// Lists the installations recorded in the guide state: one removable card per
    /// environment and skill target.
    /// </summary>
    public async Task<IReadOnlyList<SetupInstallationView>> ListInstallationsAsync(
        CancellationToken cancellationToken = default)
    {
        var installations = await Task.Run(() => CreateService().ListRecordedInstallations(), cancellationToken);
        return installations
            .Select(static installation => new SetupInstallationView(
                installation.Environment.Selector,
                installation.Target,
                installation.TargetRoot,
                installation.SkillCount,
                installation.Workspace))
            .ToArray();
    }

    /// <summary>Lists every registered workspace with its live health observation.</summary>
    public async Task<IReadOnlyList<SetupWorkspaceView>> ListWorkspacesAsync(
        CancellationToken cancellationToken = default)
    {
        var views = await Task.Run(
            () => new GuideWorkspaceService(Product, GuidePaths.ForCurrentUser(), LoadWorkspaceCatalog())
                .ListWorkspaces(),
            cancellationToken);
        return views
            .Select(static view => new SetupWorkspaceView(
                view.Workspace,
                view.Profile,
                view.Capabilities,
                view.DirectoryExists,
                view.ConfigurationCurrent,
                view.InstalledSkillCount,
                view.ProfileSkillCount,
                view.InstructionsCurrent,
                view.Issues))
            .ToArray();
    }

    /// <summary>Advisory detection for one workspace candidate the user typed or pasted.</summary>
    public async Task<SetupWorkspaceDetectionView> InspectWorkspaceAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new SetupWorkspaceDetectionView(
                GuideWorkspaceDetectionOutcome.NotADirectory, null, string.Empty, string.Empty,
                [], null, false, string.Empty);
        }

        return await Task.Run(() =>
        {
            var candidate = new GuideWorkspaceService(Product, GuidePaths.ForCurrentUser(), LoadWorkspaceCatalog())
                .DetectCandidate(path);
            return new SetupWorkspaceDetectionView(
                candidate.Outcome,
                candidate.CandidateProfile,
                candidate.Confidence,
                candidate.Reason,
                candidate.Capabilities,
                candidate.FrameworkVersion,
                candidate.Initialized,
                string.Empty);
        }, cancellationToken);
    }

    /// <summary>Previews installing the workspace's configured profile closure into its project directories.</summary>
    public async Task<SetupOperationView> PreviewWorkspaceInstallAsync(
        string workspace,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
        => await WorkspaceConfigureAsync(workspace, null, progress, cancellationToken);

    /// <summary>Applies the digest-locked workspace skill installation plan.</summary>
    public async Task<SetupOperationView> ApplyWorkspaceInstallAsync(
        string workspace,
        string planDigest,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
        => await WorkspaceConfigureAsync(workspace, planDigest, progress, cancellationToken);

    /// <summary>Previews initializing a workspace (configuration, instruction block, registry entry).</summary>
    public async Task<SetupOperationView> PreviewWorkspaceInitAsync(
        string workspace,
        string profile,
        IReadOnlyList<string> capabilities,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
        => await WorkspaceInitAsync(workspace, profile, capabilities, null, progress, cancellationToken);

    /// <summary>Applies the digest-locked workspace initialization plan.</summary>
    public async Task<SetupOperationView> ApplyWorkspaceInitAsync(
        string workspace,
        string profile,
        IReadOnlyList<string> capabilities,
        string planDigest,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
        => await WorkspaceInitAsync(workspace, profile, capabilities, planDigest, progress, cancellationToken);

    /// <summary>Previews forgetting one workspace: guide artifacts and project skill installations.</summary>
    public async Task<SetupOperationView> PreviewWorkspaceForgetAsync(
        string workspace,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
        => await WorkspaceForgetAsync(workspace, null, progress, cancellationToken);

    /// <summary>Applies the digest-locked forget plan.</summary>
    public async Task<SetupOperationView> ApplyWorkspaceForgetAsync(
        string workspace,
        string planDigest,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
        => await WorkspaceForgetAsync(workspace, planDigest, progress, cancellationToken);

    private async Task<SetupOperationView> WorkspaceConfigureAsync(
        string workspace,
        string? planDigest,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var bundle = DetectDefaultBundle();
            if (bundle is null)
            {
                return ErrorView($"{Product.DisplayName} is not installed yet; no release bundle is available to install from.");
            }

            var request = new GuideConfigureRequest(null, null, [], Workspace: workspace);
            using var service = CreateService(Path.Combine(bundle, "app"));
            var report = await Task.Run(
                () => planDigest is null
                    ? service.PreviewConfigureAsync(request, progress, cancellationToken)
                    : service.ApplyConfigureAsync(request, planDigest, progress, cancellationToken),
                cancellationToken);
            return ToOperationView(report);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ErrorView(exception.Message);
        }
    }

    private async Task<SetupOperationView> WorkspaceInitAsync(
        string workspace,
        string profile,
        IReadOnlyList<string> capabilities,
        string? planDigest,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var service = new GuideWorkspaceService(Product, GuidePaths.ForCurrentUser(), LoadWorkspaceCatalog());
            var request = new GuideWorkspaceInitRequest(workspace, profile, capabilities);
            var report = await Task.Run(
                () => planDigest is null
                    ? service.InitAsync(request, null, progress, cancellationToken)
                    : service.InitAsync(request, planDigest, progress, cancellationToken),
                cancellationToken);
            return ToOperationView(report);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ErrorView(exception.Message);
        }
    }

    private async Task<SetupOperationView> WorkspaceForgetAsync(
        string workspace,
        string? planDigest,
        IProgress<GuidePhase>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var service = new GuideWorkspaceService(Product, GuidePaths.ForCurrentUser(), LoadWorkspaceCatalog());
            var report = await Task.Run(
                () => planDigest is null
                    ? service.ForgetAsync(workspace, null, progress, cancellationToken)
                    : service.ForgetAsync(workspace, planDigest, progress, cancellationToken),
                cancellationToken);
            return ToOperationView(report);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ErrorView(exception.Message);
        }
    }

    private SkillCatalog? LoadWorkspaceCatalog()
        => GuideWorkspaceService.TryLoadProductCatalog(Product, GuidePaths.ForCurrentUser());

    /// <summary>Previews removal of the selected guide-owned skill targets (all when null).</summary>
    public async Task<SetupOperationView> PreviewUninstallAsync(
        GuideUnconfigureRequest request,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var service = CreateService();
            var report = await Task.Run(
                () => service.PreviewUnconfigureAsync(request, progress, cancellationToken),
                cancellationToken);
            return ToOperationView(report);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ErrorView(exception.Message);
        }
    }

    /// <summary>Applies a digest-locked uninstall plan, then removes owned shell conveniences.</summary>
    public async Task<SetupOperationView> ApplyUninstallAsync(
        GuideUnconfigureRequest request,
        string planDigest,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var locator = ReadLocator();
            using var service = CreateService();
            var report = await Task.Run(
                () => service.ApplyUnconfigureAsync(request, planDigest, progress, cancellationToken),
                cancellationToken);
            if (report.Plan?.Applied == true
                && request.Targets is null
                && request.Environments is null
                && locator is not null)
            {
                RemoveDesktopIntegration(locator);
            }
            return ToOperationView(report);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ErrorView(exception.Message);
        }
    }

    /// <summary>Starts the installed product detached and reports whether it became reachable.</summary>
    public bool TryStartProduct(out string? error)
    {
        error = null;
        if (!Product.ServesLoopback)
        {
            error = $"{Product.DisplayName} runs no local service.";
            return false;
        }
        var locator = ReadLocator();
        if (locator is null || !File.Exists(locator.ExecutablePath))
        {
            error = $"The installed {Product.DisplayName} executable was not found.";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(
                locator.ExecutablePath,
                Product.BackgroundStartArguments ?? string.Empty)
            {
                UseShellExecute = false,
                CreateNoWindow = false
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or FileNotFoundException)
        {
            error = exception.Message;
            return false;
        }

        for (var attempt = 0; attempt < 15 && !IsPortListening(CurrentPort()); attempt++)
        {
            Thread.Sleep(1_000);
        }

        return IsPortListening(CurrentPort());
    }

    /// <summary>Stops product processes launched from the installed executable and waits for the port to free.</summary>
    public bool TryStopProduct(out string? error)
    {
        error = null;
        var locator = ReadLocator();
        if (locator is null)
        {
            error = $"{Product.DisplayName} is not installed.";
            return false;
        }

        return TryStopProductAt(locator.ExecutablePath, out error);
    }

    /// <summary>Stops product processes launched from one executable path, e.g. the superseded release.</summary>
    public bool TryStopProductAt(string executablePath, out string? error)
    {
        error = null;
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Product.ServesLoopback)
        {
            return true;
        }
        var port = CurrentPort();
        if (!IsPortListening(port))
        {
            return true;
        }

        var executable = Path.TrimEndingDirectorySeparator(Path.GetFullPath(executablePath));
        // Entry file names are platform-specific; the running platform's name is the one
        // this wizard could have started.
        var processName = Path.GetFileNameWithoutExtension(
            Product.CurrentPlatform()?.ExecutableName ?? executable);
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var targets = new List<Process>();
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                if (string.Equals(process.MainModule?.FileName, executable, pathComparison))
                {
                    targets.Add(process);
                }
            }
            catch (Win32Exception)
            {
                // A process started elevated cannot be inspected or stopped from this process.
            }
            finally
            {
                process.Dispose();
            }
        }

        foreach (var process in targets)
        {
            using (process)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(10_000);
                }
                catch (Win32Exception)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        for (var attempt = 0; attempt < 10 && IsPortListening(port); attempt++)
        {
            Thread.Sleep(500);
        }

        if (IsPortListening(port))
        {
            error = "The product port is still busy after stopping the installed processes.";
            return false;
        }

        return true;
    }

    /// <summary>Queries the release feed and compares the latest release with the installed product.</summary>
    public async Task<SetupUpdateFeedView> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (Product.GitHubSlug is null || Product.ArchiveAssetNameTemplate.Length == 0)
        {
            return new SetupUpdateFeedView(
                false, null, null, null, null, null, null, 0,
                $"{Product.DisplayName} declares no release feed.");
        }
        var locator = await Task.Run(ReadLocator, cancellationToken);
        if (locator is null)
        {
            return new SetupUpdateFeedView(
                false, null, null, null, null, null, null, 0, $"{Product.DisplayName} is not installed yet.");
        }

        using var feed = new GuideUpdateFeed(
            Product.GitHubSlug,
            Product.ArchiveAssetNameTemplate,
            null,
            await Task.Run(GithubCredential.ReadToken, cancellationToken));
        var check = await feed.CheckLatestAsync(locator.ProductVersion, cancellationToken);
        return new SetupUpdateFeedView(
            check.UpdateAvailable,
            check.InstalledVersion,
            check.Latest?.Version,
            check.Latest?.Tag,
            check.Latest?.Title,
            check.Latest?.Notes,
            check.Latest?.PublishedAt,
            check.Latest?.ArchiveSizeBytes ?? 0,
            check.Error);
    }

    /// <summary>Downloads, checksum-verifies, extracts, and validates one published release.</summary>
    public async Task<SetupStagedUpdateView> StageUpdateAsync(
        SetupUpdateFeedView update,
        IProgress<SetupProgressView>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var locator = ReadLocator();
        if (locator?.BundleRoot is null)
        {
            return new SetupStagedUpdateView(
                GuideSetupPresenter.PresentBundle(null), $"{Product.DisplayName} is not installed yet.");
        }

        using var feed = new GuideUpdateFeed(
            Product.GitHubSlug ?? throw new InvalidOperationException("The product declares no release feed."),
            Product.ArchiveAssetNameTemplate,
            null,
            GithubCredential.ReadToken());
        var check = await feed.CheckLatestAsync(locator.ProductVersion, cancellationToken);
        if (check.Latest is null)
        {
            return new SetupStagedUpdateView(
                GuideSetupPresenter.PresentBundle(null), check.Error ?? "No release is available.");
        }

        using var stager = new GuideUpdateStager(Product, null, GithubCredential.ReadToken());
        var staged = await stager.StageAsync(
            check.Latest,
            locator.BundleRoot,
            progress is null ? null : new Progress<GuideUpdateProgress>(value =>
                progress.Report(new SetupProgressView(value.Phase, value.Completed, value.Total))),
            cancellationToken);
        if (!staged.Succeeded || staged.BundleRoot is null)
        {
            return new SetupStagedUpdateView(GuideSetupPresenter.PresentBundle(null), staged.Error);
        }

        progress?.Report(new SetupProgressView("validate", 0, 1));
        var view = await Task.Run(() => GuideSetupPresenter.PresentBundle(staged.BundleRoot), cancellationToken);
        if (view.Error is not null)
        {
            return new SetupStagedUpdateView(view, view.Error);
        }

        session.StagedUpdateBundle = view;
        progress?.Report(new SetupProgressView("validate", 1, 1));
        return new SetupStagedUpdateView(view, null);
    }

    /// <summary>Validated release bundles kept beside the current install, newest first.</summary>
    public async Task<IReadOnlyList<SetupBundleView>> ListRollbackBundlesAsync(
        CancellationToken cancellationToken = default)
    {
        var locator = await Task.Run(ReadLocator, cancellationToken);
        if (locator?.BundleRoot is null || Product.BundleDirectoryPrefix.Length == 0)
        {
            return [];
        }

        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(locator.BundleRoot));
        var stagedRoot = session.StagedUpdateBundle is { Exists: true } view
            ? Path.GetDirectoryName(view.ApplicationDirectory)
            : null;
        var siblings = await Task.Run(
            () => GuideSetupPresenter.PresentSiblingBundles(current, Product.BundleDirectoryPrefix),
            cancellationToken);
        return siblings
            .Where(view => !string.Equals(
                Path.GetDirectoryName(view.ApplicationDirectory),
                current,
                StringComparison.OrdinalIgnoreCase))
            .Where(view => stagedRoot is null || !string.Equals(
                Path.GetDirectoryName(view.ApplicationDirectory),
                Path.GetFullPath(stagedRoot),
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(
                view => view.ProductVersion ?? string.Empty,
                Comparer<string>.Create(static (left, right) => GuideUpdateFeed.CompareVersions(left, right)))
            .ToArray();
    }

    /// <summary>Reads the current convenience shell integration for the installed executable.</summary>
    public SetupDesktopIntegrationView LoadDesktopIntegration()
    {
        var locator = ReadLocator();
        if (locator is null || !OperatingSystem.IsWindows())
        {
            // Shortcuts and autostart are a Windows-only convenience; other hosts never see it.
            return new SetupDesktopIntegrationView(false, false, false, false, null);
        }

        try
        {
            var integration = CreateDesktopIntegration();
            return new SetupDesktopIntegrationView(
                true,
                integration.ShortcutExists(GuideShortcutSite.Desktop),
                integration.ShortcutExists(GuideShortcutSite.StartMenu),
                integration.IsAutoStartEnabled(locator.ExecutablePath),
                null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new SetupDesktopIntegrationView(true, false, false, false, exception.Message);
        }
    }

    /// <summary>Reconciles shortcuts and autostart to the requested state for the installed executable.</summary>
    public SetupDesktopIntegrationView ApplyDesktopIntegration(
        bool desktopShortcut,
        bool startMenuShortcut,
        bool autoStart)
    {
        var locator = ReadLocator();
        if (locator is null)
        {
            return new SetupDesktopIntegrationView(false, false, false, false, $"{Product.DisplayName} is not installed yet.");
        }

        if (!OperatingSystem.IsWindows())
        {
            return new SetupDesktopIntegrationView(false, false, false, false, null);
        }

        try
        {
            var integration = CreateDesktopIntegration();
            foreach (var (site, desired) in new[]
                     {
                         (GuideShortcutSite.Desktop, desktopShortcut),
                         (GuideShortcutSite.StartMenu, startMenuShortcut)
                     })
            {
                if (desired)
                {
                    integration.CreateShortcut(site, locator.ExecutablePath);
                }
                else
                {
                    integration.RemoveShortcutIfOwned(site, locator.ExecutablePath);
                }
            }

            if (autoStart)
            {
                integration.EnableAutoStart(locator.ExecutablePath);
            }
            else
            {
                integration.DisableAutoStartIfOwned(locator.ExecutablePath);
            }

            return new SetupDesktopIntegrationView(
                true,
                integration.ShortcutExists(GuideShortcutSite.Desktop),
                integration.ShortcutExists(GuideShortcutSite.StartMenu),
                integration.IsAutoStartEnabled(locator.ExecutablePath),
                null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return new SetupDesktopIntegrationView(true, false, false, false, exception.Message);
        }
    }

    private GuideDesktopIntegration CreateDesktopIntegration() => new(
        Product.DisplayName,
        Product.ProductName,
        Product.ShortcutArguments ?? string.Empty,
        Product.BackgroundStartArguments ?? string.Empty);

    /// <summary>Keeps existing conveniences coherent when the installed executable path moves to a new release.</summary>
    private void RepointDesktopIntegrationAfterInstall(
        GuideInstallationLocator? previous,
        GuideInstallationLocator? current)
    {
        if (current is null || !OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var integration = CreateDesktopIntegration();
            if (previous is null)
            {
                // A first install does not touch shell integration here; the wizard applies
                // the shortcut/autostart choices the user made on its own summary step.
                return;
            }

            // Only conveniences that already existed for the old release are carried over,
            // so an update never silently adds shell integration the user did not choose.
            var desktop = integration.ShortcutExists(GuideShortcutSite.Desktop);
            var startMenu = integration.ShortcutExists(GuideShortcutSite.StartMenu);
            var autoStart = integration.IsAutoStartEnabled(previous.ExecutablePath);
            integration.RemoveShortcutIfOwned(GuideShortcutSite.Desktop, previous.ExecutablePath);
            integration.RemoveShortcutIfOwned(GuideShortcutSite.StartMenu, previous.ExecutablePath);
            integration.DisableAutoStartIfOwned(previous.ExecutablePath);
            if (desktop) integration.CreateShortcut(GuideShortcutSite.Desktop, current.ExecutablePath);
            if (startMenu) integration.CreateShortcut(GuideShortcutSite.StartMenu, current.ExecutablePath);
            if (autoStart) integration.EnableAutoStart(current.ExecutablePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // Convenience repointing must never fail the digest-locked install that already applied.
        }
    }

    private void RemoveDesktopIntegration(GuideInstallationLocator locator)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var integration = CreateDesktopIntegration();
            integration.RemoveShortcutIfOwned(GuideShortcutSite.Desktop, locator.ExecutablePath);
            integration.RemoveShortcutIfOwned(GuideShortcutSite.StartMenu, locator.ExecutablePath);
            integration.DisableAutoStartIfOwned(locator.ExecutablePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // Uninstall already removed governed configuration; leftover conveniences are harmless.
        }
    }

    private static SetupOperationView ToOperationView(GuideReport report)
        => new(
            report.Plan?.Applied ?? false,
            report.Status,
            GuideSetupPresenter.PresentChecks(report.Plan?.Checks ?? report.Checks, report.Status),
            GuideSetupPresenter.PresentPlan(report.Plan),
            report.Plan?.PlanDigest,
            report.Status == GuideStatus.Error ? report.Summary.Message : null);

    private static SetupOperationView ErrorView(string message)
        => new(false, GuideStatus.Error, new SetupChecksView(GuideStatus.Error, []), new SetupPlanView(false, 0, []), null, message);

    private GuideInstallationLocator? ReadLocator()
    {
        try
        {
            var path = AgentProductPaths.ForCurrentUser(Product).InstallationLocatorFile;
            return File.Exists(path)
                ? JsonSerializer.Deserialize<GuideInstallationLocator>(File.ReadAllText(path), LocatorJson)
                : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Callers poll inside Task.Run-bounded start/stop sequences, so a blocking bridge is safe.
    private static bool IsPortListening(int port)
        => IsPortListeningAsync(port).GetAwaiter().GetResult();

    private static async Task<bool> IsPortListeningAsync(int port)
    {
        foreach (var address in new[] { System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback })
        {
            try
            {
                using var client = new TcpClient(address.AddressFamily);
                await client.ConnectAsync(address, port, CancellationToken.None)
                    .AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(1));
                return true;
            }
            catch (Exception exception) when (exception is SocketException or TimeoutException or TaskCanceledException)
            {
            }
        }

        return false;
    }
}
