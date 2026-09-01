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

        internal SkillCatalog LoadCatalog()
            => GuideReleaseMetadata.LoadSkillCatalog(SkillsRoot);

        /// <summary>Creates a workspace directory with a guide configuration for one profile.</summary>
        internal string CreateWorkspace(string profile, string[]? skillTargets = null)
        {
            var workspace = Path.Combine(_root, "workspaces", $"repo-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workspace);
            if (profile is not null)
            {
                var config = new GuideWorkspaceStore.GuideWorkspaceConfig(
                    GuideWorkspaceStore.GuideWorkspaceConfig.CurrentSchemaVersion,
                    Product.ProductId,
                    profile,
                    [],
                    1,
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
