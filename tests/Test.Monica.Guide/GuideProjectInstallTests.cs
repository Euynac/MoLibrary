using System.Text;
using System.Text.Json;
using Monica.Guide;
using Xunit;

namespace Test.Monica.Guide;

public sealed class GuideProjectInstallTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ConfigureWorkspace_InstallsProfileClosureIntoProjectDirectory()
    {
        using var fixture = new ProjectFixture();
        var workspace = fixture.CreateWorkspace("monica-application");
        using var service = fixture.CreateService();

        var request = new GuideConfigureRequest(null, null, [], Workspace: workspace);
        var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);

        // First configure on a clean ledger keeps the informational not-yet-recorded warning.
        Assert.NotEqual(GuideStatus.Error, preview.Status);
        Assert.All(
            preview.Plan!.Actions.Where(action => action.Kind == GuidePlanActionKind.ReplaceSkillDirectory),
            action => Assert.StartsWith(Path.Combine(workspace, ".agents", "skills"), action.Target, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(preview.Plan.Actions, action => action.Id == "workspace.registry.write");
        Assert.False(Directory.Exists(fixture.ProjectSkillRoot(workspace)));

        var applied = await service.ApplyConfigureAsync(request, preview.Plan.PlanDigest, cancellationToken: CancellationToken);
        Assert.True(applied.Plan!.Applied);
        Assert.True(File.Exists(Path.Combine(fixture.ProjectSkillRoot(workspace), "monica-application", "SKILL.md")));
        // The global shared catalog stays untouched: project installs are opt-out of global.
        Assert.False(Directory.Exists(fixture.GlobalSharedRoot));

        var installation = fixture.ProductInstallations().Single();
        Assert.Equal(GuideTarget.Project, installation.Target);
        Assert.Equal(workspace, installation.WorkspaceRoot);
        Assert.Equal(fixture.ProjectSkillRoot(workspace), installation.TargetRoot);
    }

    [Fact]
    public async Task ConfigureWorkspace_FromSetupTreeBundlesResolvesTheParentCatalog()
    {
        using var fixture = new ProjectFixture();
        // Two-tree bundle layout: the guide executable ships in setup/ beside the product app,
        // with the skills catalog and release manifest at the shared bundle root.
        var setupDirectory = Path.Combine(fixture.BundleRoot, "setup");
        Directory.CreateDirectory(setupDirectory);
        File.WriteAllText(Path.Combine(setupDirectory, "Monica.Guide.exe"), "test executable marker");
        File.WriteAllText(
            Path.Combine(fixture.BundleRoot, "release-manifest.json"),
            """{"schemaVersion":1}""");
        using var service = fixture.CreateSetupTreeService(setupDirectory);
        var workspace = fixture.CreateWorkspace("monica-application");

        var request = new GuideConfigureRequest(null, null, [], Workspace: workspace);
        var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);

        Assert.Contains(preview.Checks, check =>
            check.Id == "skills.catalog" && check.Status == GuideCheckStatus.Ok);
        Assert.Contains(preview.Plan!.Actions, action =>
            action.Kind == GuidePlanActionKind.ReplaceSkillDirectory
            && action.Target.StartsWith(Path.Combine(workspace, ".agents", "skills"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Configure_FromAWorkflowBundleRecordsTheProductProgramEntry()
    {
        using var fixture = new ProjectFixture();
        var workspace = fixture.CreateWorkspace("monica-application", product: KnownAgentProducts.MonicaWorkflow);
        using var service = fixture.CreateWorkflowBundleService();
        var request = new GuideConfigureRequest(
            null,
            new Uri("http://localhost:61345/"),
            [],
            Workspace: workspace);

        var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
        var applied = await service.ApplyConfigureAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);

        Assert.True(applied.Plan!.Applied);
        // The recorded program is the app/ entry, not the setup/ guide executable that ran
        // the configure; Start Cockpit launches the recorded path with serve arguments.
        var ledger = JsonSerializer.Deserialize<GuideLedger>(
            await File.ReadAllTextAsync(fixture.EnginePaths.GuideLedgerFile, CancellationToken),
            GuidePlanning.JsonOptions);
        var state = ledger!.Products.Single(
            product => product.ProductId == KnownAgentProducts.MonicaWorkflow.ProductId);
        Assert.EndsWith(
            Path.Combine("app", "Monica.Workflow.exe"),
            state.ExecutablePath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfigureWorkspace_CarriesTheRecordedLoopbackAddressWhenTheRequestStaysSilent()
    {
        using var fixture = new ProjectFixture();
        var workspace = fixture.CreateWorkspace("monica-application", product: KnownAgentProducts.MonicaWorkflow);
        var request = new GuideConfigureRequest(
            null, new Uri("http://localhost:61345/"), [], Workspace: workspace);
        using (var first = fixture.CreateWorkflowBundleService())
        {
            var preview = await first.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
            await first.ApplyConfigureAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);
        }

        Assert.True(File.Exists(fixture.WorkflowProductPaths.ServerConfigurationFile));

        // A silent workspace update carries the recorded loopback decision instead of
        // re-asking for a port this machine already selected.
        using var service = fixture.CreateWorkflowBundleService();
        var silent = new GuideConfigureRequest(null, null, [], Workspace: workspace);
        var report = await service.PreviewConfigureAsync(silent, cancellationToken: CancellationToken);

        Assert.DoesNotContain(report.Checks, check => check.Id == "server.base-address");
        var applied = await service.ApplyConfigureAsync(silent, report.Plan!.PlanDigest, cancellationToken: CancellationToken);
        Assert.True(applied.Plan!.Applied);

        var ledger = JsonSerializer.Deserialize<GuideLedger>(
            await File.ReadAllTextAsync(fixture.EnginePaths.GuideLedgerFile, CancellationToken),
            GuidePlanning.JsonOptions);
        var state = ledger!.Products.Single(
            product => product.ProductId == KnownAgentProducts.MonicaWorkflow.ProductId);
        Assert.Equal(61345, state.BaseAddress?.Port);
    }

    [Fact]
    public async Task ConfigureWorkspace_HonorsCustomSkillTargetList()
    {
        using var fixture = new ProjectFixture();
        var workspace = fixture.CreateWorkspace("monica-application", skillTargets: [".agents/skills", ".claude/skills"]);
        using var service = fixture.CreateService();
        var request = new GuideConfigureRequest(null, null, [], Workspace: workspace);

        var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
        var applied = await service.ApplyConfigureAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);

        Assert.True(applied.Plan!.Applied);
        var roots = fixture.ProductInstallations().Select(static installation => installation.TargetRoot).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal(
            [Path.Combine(workspace, ".agents", "skills"), Path.Combine(workspace, ".claude", "skills")],
            roots);
        Assert.True(File.Exists(Path.Combine(workspace, ".claude", "skills", "monica-application", "SKILL.md")));
    }

    [Fact]
    public async Task ConfigureWorkspace_RejectsEscapingSkillTargets()
    {
        using var fixture = new ProjectFixture();
        var workspace = fixture.CreateWorkspace("monica-application", skillTargets: ["../outside"]);
        using var service = fixture.CreateService();

        var preview = await service.PreviewConfigureAsync(
            new GuideConfigureRequest(null, null, [], Workspace: workspace),
            cancellationToken: CancellationToken);

        Assert.Equal(GuideStatus.Error, preview.Status);
        Assert.Contains(preview.Checks, check => check.Id == "workspace.skill-targets");
    }

    [Fact]
    public async Task ConfigureWorkspace_RequiresInitializationAndSupportsProfileOverride()
    {
        using var fixture = new ProjectFixture();
        var uninitialized = fixture.CreateWorkspace(null!);
        using var service = fixture.CreateService();

        var missing = await service.PreviewConfigureAsync(
            new GuideConfigureRequest(null, null, [], Workspace: uninitialized),
            cancellationToken: CancellationToken);
        Assert.Equal(GuideStatus.Error, missing.Status);
        Assert.Contains(missing.Checks, check => check.Id == "workspace.profile");

        var workspace = fixture.CreateWorkspace("monica-extension");
        var overridden = await service.PreviewConfigureAsync(
            new GuideConfigureRequest(null, null, [], Profile: "monica-application", Workspace: workspace),
            cancellationToken: CancellationToken);
        Assert.NotEqual(GuideStatus.Error, overridden.Status);
        Assert.Contains(overridden.Plan!.Actions, action => action.Target.Contains("monica-application", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TwoWorkspacesWithDifferentProfilesCoexistAndRefreshTogether()
    {
        using var fixture = new ProjectFixture();
        var application = fixture.CreateWorkspace("monica-application");
        var extension = fixture.CreateWorkspace("monica-extension");
        using var service = fixture.CreateService();
        foreach (var workspace in new[] { application, extension })
        {
            var request = new GuideConfigureRequest(null, null, [], Workspace: workspace);
            var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
            await service.ApplyConfigureAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);
        }

        Assert.Equal(2, fixture.ProductInstallations().Count);

        // The classic no-selection refresh replays every recorded installation, project and
        // global alike, so release updates reach project directories automatically.
        var refresh = await service.PreviewConfigureAsync(
            new GuideConfigureRequest(null, null, []),
            cancellationToken: CancellationToken);
        Assert.Contains(refresh.Plan!.Actions, action => action.Target.Contains(application, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(refresh.Plan.Actions, action => action.Target.Contains(extension, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UnconfigureWorkspace_RemovesOnlyThatWorkspaceInstallations()
    {
        using var fixture = new ProjectFixture();
        var kept = fixture.CreateWorkspace("monica-application");
        var removed = fixture.CreateWorkspace("monica-extension");
        using var service = fixture.CreateService();
        foreach (var workspace in new[] { kept, removed })
        {
            var request = new GuideConfigureRequest(null, null, [], Workspace: workspace);
            var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
            await service.ApplyConfigureAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);
        }

        var unconfigure = await service.PreviewUnconfigureAsync(
            new GuideUnconfigureRequest(Workspace: removed), cancellationToken: CancellationToken);
        var applied = await service.ApplyUnconfigureAsync(
            new GuideUnconfigureRequest(Workspace: removed), unconfigure.Plan!.PlanDigest, cancellationToken: CancellationToken);

        Assert.True(applied.Plan!.Applied);
        Assert.False(Directory.Exists(fixture.ProjectSkillRoot(removed)));
        Assert.True(File.Exists(Path.Combine(fixture.ProjectSkillRoot(kept), "monica-application", "SKILL.md")));
        var remaining = fixture.ProductInstallations().Single();
        Assert.Equal(kept, remaining.WorkspaceRoot);
    }

    [Fact]
    public async Task Forget_RemovesProjectSkillsConfigurationAndRegistryEntry()
    {
        using var fixture = new ProjectFixture();
        var workspace = fixture.CreateWorkspace("monica-application");
        using var service = fixture.CreateService();
        var installPreview = await service.PreviewConfigureAsync(
            new GuideConfigureRequest(null, null, [], Workspace: workspace),
            cancellationToken: CancellationToken);
        await service.ApplyConfigureAsync(
            new GuideConfigureRequest(null, null, [], Workspace: workspace),
            installPreview.Plan!.PlanDigest,
            cancellationToken: CancellationToken);
        var workspaceService = new GuideWorkspaceService(ProjectFixture.Product, fixture.EnginePaths, fixture.LoadCatalog());

        var forget = await workspaceService.ForgetAsync(workspace, cancellationToken: CancellationToken);
        var applied = await workspaceService.ForgetAsync(workspace, forget.Plan!.PlanDigest, cancellationToken: CancellationToken);

        Assert.True(applied.Plan!.Applied);
        Assert.False(Directory.Exists(fixture.ProjectSkillRoot(workspace)));
        Assert.False(File.Exists(GuideWorkspaceStore.ConfigPath(workspace)));
        Assert.Empty(fixture.ProductInstallations());
        Assert.Empty(workspaceService.ListWorkspaces());
    }

    [Fact]
    public async Task ConfigureWorkspace_ConvergesGuideSkillWithGlobalFirstPreference()
    {
        using var fixture = new ProjectFixture();
        var workspace = fixture.CreateWorkspace("monica-application");
        var request = new GuideConfigureRequest(null, null, [], Workspace: workspace);
        var guideDirectory = Path.Combine(fixture.ProjectSkillRoot(workspace), ProjectFixture.GuideSkill);

        void SetPreference(bool enabled)
            => GuidePreferencesStore.Save(
                ProjectFixture.Product,
                new GuidePreferences(GuidePreferences.CurrentSchemaVersion, enabled),
                fixture.ProductPaths);

        // Preference off: the workspace closure installs the guide skill locally.
        SetPreference(false);
        using (var service = fixture.CreateService())
        {
            var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
            await service.ApplyConfigureAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);
        }

        Assert.True(Directory.Exists(guideDirectory));

        // Enabling global-first makes the next workspace update remove the local copy.
        SetPreference(true);
        using (var service = fixture.CreateService())
        {
            var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
            Assert.Contains(preview.Plan!.Actions, action =>
                action.Kind == GuidePlanActionKind.DeleteSkillDirectory
                && action.Target.EndsWith(ProjectFixture.GuideSkill, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(preview.Plan.Actions, action =>
                action.Kind == GuidePlanActionKind.ReplaceSkillDirectory
                && action.Target.EndsWith(ProjectFixture.GuideSkill, StringComparison.OrdinalIgnoreCase));
            var applied = await service.ApplyConfigureAsync(request, preview.Plan.PlanDigest, cancellationToken: CancellationToken);
            Assert.True(applied.Plan!.Applied);
        }

        Assert.False(Directory.Exists(guideDirectory));
        Assert.True(Directory.Exists(Path.Combine(fixture.ProjectSkillRoot(workspace), ProjectFixture.ApplicationSkill)));

        // Disabling again restores the local copy on the next workspace update.
        SetPreference(false);
        using (var service = fixture.CreateService())
        {
            var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
            Assert.Contains(preview.Plan!.Actions, action =>
                action.Kind == GuidePlanActionKind.ReplaceSkillDirectory
                && action.Target.EndsWith(ProjectFixture.GuideSkill, StringComparison.OrdinalIgnoreCase));
            var applied = await service.ApplyConfigureAsync(request, preview.Plan.PlanDigest, cancellationToken: CancellationToken);
            Assert.True(applied.Plan!.Applied);
        }

        Assert.True(Directory.Exists(guideDirectory));
    }

    [Fact]
    public async Task ConfigureWorkspace_ConvergesMachineProjectionIntoInstructions()
    {
        using var fixture = new ProjectFixture();
        var workspace = fixture.CreateWorkspace("monica-application");
        var request = new GuideConfigureRequest(null, null, [], Workspace: workspace);
        var agentsFile = Path.Combine(workspace, "AGENTS.md");

        void SetProjection(bool sourceHints, bool issuePolicy)
            => GuideWorkspaceProjectionStore.Save(
                fixture.EnginePaths,
                new GuideWorkspaceProjection(GuideWorkspaceProjection.CurrentSchemaVersion, sourceHints, issuePolicy));

        async Task ApplyAsync()
        {
            using var service = fixture.CreateService();
            var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
            await service.ApplyConfigureAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);
        }

        // Defaults on, no binding yet: the first update creates the block with the issue policy.
        await ApplyAsync();
        var agents = await File.ReadAllTextAsync(agentsFile, CancellationToken);
        Assert.Contains("## Issue reporting policy", agents, StringComparison.Ordinal);
        Assert.DoesNotContain("First-party source on this machine", agents, StringComparison.Ordinal);

        // Binding a verified source makes the next update project the locator into the block.
        var root = Path.GetDirectoryName(fixture.BundleRoot)!;
        var checkout = Path.Combine(root, "checkout");
        Directory.CreateDirectory(checkout);
        var source = new GuideSourceService(fixture.EnginePaths, null, new CanonicalCheckoutProbe(checkout));
        var bindRequest = new GuideSourceBindRequest("monica", checkout, null);
        var bind = await source.BindAsync(bindRequest, cancellationToken: CancellationToken);
        await source.BindAsync(bindRequest, bind.Plan!.PlanDigest, cancellationToken: CancellationToken);

        using (var service = fixture.CreateService())
        {
            var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
            Assert.Contains(preview.Plan!.Actions, action =>
                action.Kind == GuidePlanActionKind.WriteFile
                && action.Target.EndsWith("AGENTS.md", StringComparison.OrdinalIgnoreCase));
            await service.ApplyConfigureAsync(request, preview.Plan.PlanDigest, cancellationToken: CancellationToken);
        }

        agents = await File.ReadAllTextAsync(agentsFile, CancellationToken);
        Assert.Contains("First-party source on this machine", agents, StringComparison.Ordinal);
        Assert.Contains(checkout, agents, StringComparison.Ordinal);

        // Turning both switches off removes the sections with the next update.
        SetProjection(false, false);
        await ApplyAsync();
        agents = await File.ReadAllTextAsync(agentsFile, CancellationToken);
        Assert.DoesNotContain("First-party source on this machine", agents, StringComparison.Ordinal);
        Assert.DoesNotContain("## Issue reporting policy", agents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListWorkspaces_FlagsStaleInstructionsAfterAProjectionChange()
    {
        using var fixture = new ProjectFixture();
        var workspace = fixture.CreateWorkspace("monica-application");
        using (var service = fixture.CreateService())
        {
            var preview = await service.PreviewConfigureAsync(
                new GuideConfigureRequest(null, null, [], Workspace: workspace),
                cancellationToken: CancellationToken);
            await service.ApplyConfigureAsync(
                new GuideConfigureRequest(null, null, [], Workspace: workspace),
                preview.Plan!.PlanDigest,
                cancellationToken: CancellationToken);
        }

        var workspaceService = new GuideWorkspaceService(ProjectFixture.Product, fixture.EnginePaths, fixture.LoadCatalog());
        var current = workspaceService.ListWorkspaces().Single();
        Assert.True(current.InstructionsCurrent);

        GuideWorkspaceProjectionStore.Save(
            fixture.EnginePaths,
            new GuideWorkspaceProjection(GuideWorkspaceProjection.CurrentSchemaVersion, SourceHints: true, IssuePolicy: false));
        workspaceService = new GuideWorkspaceService(ProjectFixture.Product, fixture.EnginePaths, fixture.LoadCatalog());
        var stale = workspaceService.ListWorkspaces().Single();

        Assert.False(stale.InstructionsCurrent);
        Assert.Contains(stale.Issues, issue => issue.Contains("stale", StringComparison.Ordinal));
    }

    [Fact]
    public void GuidePreferences_DefaultToGlobalFirstAndRoundTrip()
    {
        using var fixture = new ProjectFixture();

        Assert.True(GuidePreferencesStore.Load(ProjectFixture.Product, fixture.ProductPaths).GlobalGuideSkill);

        GuidePreferencesStore.Save(
            ProjectFixture.Product,
            new GuidePreferences(GuidePreferences.CurrentSchemaVersion, GlobalGuideSkill: false),
            fixture.ProductPaths);
        Assert.False(GuidePreferencesStore.Load(ProjectFixture.Product, fixture.ProductPaths).GlobalGuideSkill);
    }

    [Fact]
    public async Task WorkspaceLifecycle_RegistersListsAndReportsSkillHealth()
    {
        using var fixture = new ProjectFixture();
        var workspace = fixture.CreateWorkspace("monica-application");
        using var service = fixture.CreateService();
        var workspaceService = new GuideWorkspaceService(ProjectFixture.Product, fixture.EnginePaths, fixture.LoadCatalog());

        Assert.Empty(workspaceService.ListWorkspaces());
        var inspected = workspaceService.Inspect(workspace);
        Assert.Contains(inspected.Checks, check =>
            check.Id == "workspace.skills" && check.Status == GuideCheckStatus.Warning);

        var install = await service.PreviewConfigureAsync(
            new GuideConfigureRequest(null, null, [], Workspace: workspace),
            cancellationToken: CancellationToken);
        await service.ApplyConfigureAsync(
            new GuideConfigureRequest(null, null, [], Workspace: workspace),
            install.Plan!.PlanDigest,
            cancellationToken: CancellationToken);

        var views = workspaceService.ListWorkspaces();
        var view = Assert.Single(views);
        Assert.Equal("monica-application", view.Profile);
        Assert.Equal(1, view.ProfileSkillCount);
        Assert.Equal(1, view.InstalledSkillCount);
        Assert.Empty(view.Issues);
        Assert.Contains(workspaceService.Inspect(workspace).Checks, check =>
            check.Id == "workspace.skills" && check.Status == GuideCheckStatus.Ok);
    }

    [Fact]
    public async Task WorkspacesCommand_ListsRegisteredWorkspacesAsOneEnvelope()
    {
        using var fixture = new ProjectFixture();
        var workspace = fixture.CreateWorkspace("monica-application");
        var workspaceService = new GuideWorkspaceService(ProjectFixture.Product, fixture.EnginePaths, fixture.LoadCatalog());
        var initPreview = await workspaceService.InitAsync(
            new GuideWorkspaceInitRequest(workspace, "monica-application"),
            cancellationToken: CancellationToken);
        await workspaceService.InitAsync(
            new GuideWorkspaceInitRequest(workspace, "monica-application"),
            initPreview.Plan!.PlanDigest,
            cancellationToken: CancellationToken);

        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await GuideCommandRunner.RunAsync(
            ProjectFixture.Product,
            ["workspaces", "--json"],
            output,
            error,
            CancellationToken,
            enginePaths: fixture.EnginePaths);

        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal(0, exitCode);
        Assert.Equal("workspaces", document.RootElement.GetProperty("summary").GetProperty("command").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("checks").EnumerateArray(),
            check => (check.GetProperty("id").GetString() ?? string.Empty).StartsWith("workspace.project-", StringComparison.Ordinal)
                     && check.GetProperty("details").GetProperty("profile").GetString() == "monica-application");
    }

    [Fact]
    public void Parser_ScopesWorkspaceAgainstGlobalSelectionOptions()
    {
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["configure", "--workspace", ".", "--target", "shared"]));
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["configure", "--workspace", ".", "--environment", "windows"]));
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["configure", "--workspace", ".", "--skill", "monica-guide"]));
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["unconfigure", "--workspace", ".", "--target", "shared"]));
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["workspaces", "--json", "--extra"]));

        var command = GuideCommandParser.Parse(["configure", "--workspace", ".", "--profile", "application"]);
        Assert.Equal("configure", command.Name);
        Assert.Equal("application", command.Profile);
    }

    private sealed class ProjectFixture : IDisposable
    {
        internal const string ApplicationSkill = "monica-application";
        internal const string ExtensionSkill = "monica-extension";
        internal const string GuideSkill = "monica-guide";
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"guide-project-{Guid.NewGuid():N}");

        internal ProjectFixture()
        {
            EnginePaths = new GuidePaths(Path.Combine(_root, "engine-data"));
            ProductPaths = new AgentProductPaths(Path.Combine(_root, "product-data"));
            BundleRoot = Path.Combine(_root, "bundle");
            SkillsRoot = Path.Combine(BundleRoot, "skills");
            ApplicationDirectory = Path.Combine(BundleRoot, "app");
            ExecutablePath = Path.Combine(ApplicationDirectory, "Monica.Guide.exe");
            Directory.CreateDirectory(ApplicationDirectory);
            File.WriteAllText(ExecutablePath, "test executable marker");
            WriteCatalog();
            Runtime = new FakeRuntime(Path.Combine(_root, "user-home"));
            WorkflowProductPaths = new AgentProductPaths(Path.Combine(_root, "workflow-product-data"));
        }

        internal static AgentProductDefinition Product => KnownAgentProducts.Monica;
        internal GuidePaths EnginePaths { get; }
        internal AgentProductPaths ProductPaths { get; }
        internal string BundleRoot { get; }
        internal string SkillsRoot { get; }
        internal string ApplicationDirectory { get; }
        internal string ExecutablePath { get; }
        internal FakeRuntime Runtime { get; }
        internal string GlobalSharedRoot => Path.Combine(Runtime.Home, ".agents", "skills");

        internal AgentGuideService CreateService()
            => new(
                Product,
                EnginePaths,
                ProductPaths,
                Runtime,
                null,
                ApplicationDirectory,
                static (_, _) => Task.FromResult(false));

        /// <summary>A service rooted in the setup/ tree of a two-tree bundle layout.</summary>
        internal AgentGuideService CreateSetupTreeService(string setupDirectory)
            => new(
                Product,
                EnginePaths,
                ProductPaths,
                Runtime,
                null,
                setupDirectory,
                static (_, _) => Task.FromResult(false));

        internal AgentProductPaths WorkflowProductPaths { get; }

        /// <summary>
        /// Builds a two-tree workflow bundle — the guide executable in setup/ beside the
        /// product program in app/, catalog at the bundle root — and returns a service for
        /// the workflow product rooted in that setup tree, mirroring a shipped bundle.
        /// </summary>
        internal AgentGuideService CreateWorkflowBundleService()
        {
            var bundleRoot = Path.Combine(_root, "workflow-bundle");
            var setupDirectory = Path.Combine(bundleRoot, "setup");
            var appDirectory = Path.Combine(bundleRoot, "app");
            Directory.CreateDirectory(setupDirectory);
            Directory.CreateDirectory(appDirectory);
            File.WriteAllText(Path.Combine(setupDirectory, "Monica.Guide.exe"), "guide executable marker");
            File.WriteAllText(Path.Combine(appDirectory, "Monica.Workflow.exe"), "program executable marker");
            CopyDirectory(SkillsRoot, Path.Combine(bundleRoot, "skills"));
            return new AgentGuideService(
                KnownAgentProducts.MonicaWorkflow,
                EnginePaths,
                WorkflowProductPaths,
                Runtime,
                null,
                // AppContext.BaseDirectory carries a trailing separator; the CLI service
                // inherits exactly this shape, so the fixture must not sanitize it.
                setupDirectory + Path.DirectorySeparatorChar,
                static (_, _) => Task.FromResult(false));
        }

        internal SkillCatalog LoadCatalog()
            => GuideReleaseMetadata.LoadSkillCatalog(SkillsRoot);

        /// <summary>Creates a workspace directory with a guide configuration for one profile.</summary>
        internal string CreateWorkspace(string profile, string[]? skillTargets = null, AgentProductDefinition? product = null)
        {
            var workspace = Path.Combine(_root, "workspaces", $"repo-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workspace);
            if (profile is not null)
            {
                var config = new GuideWorkspaceStore.GuideWorkspaceConfig(
                    GuideWorkspaceStore.GuideWorkspaceConfig.CurrentSchemaVersion,
                    (product ?? Product).ProductId,
                    profile,
                    [],
                    false,
                    skillTargets);
                Directory.CreateDirectory(Path.GetDirectoryName(GuideWorkspaceStore.ConfigPath(workspace))!);
                File.WriteAllText(
                    GuideWorkspaceStore.ConfigPath(workspace),
                    JsonSerializer.Serialize(config, GuidePlanning.JsonOptions));
            }

            return workspace;
        }

        internal string ProjectSkillRoot(string workspace)
            => Path.Combine(workspace, ".agents", "skills");

        internal IReadOnlyList<GuideSkillInstallation> ProductInstallations()
        {
            if (!File.Exists(EnginePaths.GuideLedgerFile))
            {
                return [];
            }

            var ledger = JsonSerializer.Deserialize<GuideLedger>(
                File.ReadAllText(EnginePaths.GuideLedgerFile), GuidePlanning.JsonOptions);
            return ledger?.Products.FirstOrDefault(product => product.ProductId == Product.ProductId)?.Installations ?? [];
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
            }

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), overwrite: true);
            }
        }

        private void WriteCatalog()
        {
            var entries = new List<(string Name, string Role, string[] Profiles)>();
            var aggregate = new List<(string Name, string Digest)>();
            foreach (var (name, role, profiles) in new[]
                     {
                         (ApplicationSkill, "development", new[] { "monica-application" }),
                         (ExtensionSkill, "development", new[] { "monica-extension" }),
                         (GuideSkill, "router", new[] { "monica-application" })
                     })
            {
                var skillRoot = Path.Combine(SkillsRoot, name);
                Directory.CreateDirectory(skillRoot);
                var content = Encoding.UTF8.GetBytes($"---\nname: {name}\ndescription: test\n---\n");
                File.WriteAllBytes(Path.Combine(skillRoot, "SKILL.md"), content);
                entries.Add((name, role, profiles));
                aggregate.Add((name, TreeDigest(("SKILL.md", content))));
            }

            var skills = entries
                .Select(entry => new
                {
                    name = entry.Name,
                    path = $"skills/{entry.Name}",
                    role = entry.Role,
                    files = new[] { "SKILL.md" },
                    dependencies = Array.Empty<string>(),
                    treeDigest = aggregate.First(item => item.Name == entry.Name).Digest,
                    profiles = entry.Profiles
                })
                .ToArray();
            var catalog = new
            {
                schemaVersion = 1,
                skillCount = skills.Length,
                treeDigest = CatalogDigest([.. aggregate]),
                skills,
                managedInstructions = new
                {
                    version = 1,
                    markers = new { start = "<!-- monica-guide:managed:start -->", end = "<!-- monica-guide:managed:end -->" },
                    templates = new Dictionary<string, object>
                    {
                        ["monica-application"] = new { skills = new[] { ApplicationSkill }, rules = new[] { "Use $monica-application." } },
                        ["monica-extension"] = new { skills = new[] { ExtensionSkill }, rules = new[] { "Use $monica-extension." } }
                    }
                }
            };
            File.WriteAllText(
                Path.Combine(SkillsRoot, "catalog.json"),
                JsonSerializer.Serialize(catalog, GuidePlanning.JsonOptions));
        }

        private static string TreeDigest(params (string Path, byte[] Content)[] files)
        {
            using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
            foreach (var (path, content) in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
            {
                hash.AppendData(Encoding.UTF8.GetBytes(path));
                hash.AppendData([0]);
                hash.AppendData(content);
                hash.AppendData([0]);
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        private static string CatalogDigest(params (string Name, string Digest)[] skills)
        {
            using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
            foreach (var (name, digest) in skills.OrderBy(static skill => skill.Name, StringComparer.Ordinal))
            {
                hash.AppendData(Encoding.UTF8.GetBytes(name));
                hash.AppendData([0]);
                hash.AppendData(Encoding.UTF8.GetBytes(digest));
                hash.AppendData([0]);
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
