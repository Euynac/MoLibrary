using System.Text;
using System.Text.Json;
using Monica.Guide;
using Xunit;

namespace Test.Monica.Guide;

public sealed class GuideWorkspaceTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Init_WritesConfigAndManagedInstructionBlock()
    {
        using var fixture = new WorkspaceFixture();
        var service = fixture.CreateService();
        fixture.WriteProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Monica.Core" Version="1.2.3" />
              </ItemGroup>
            </Project>
            """);

        var request = fixture.InitRequest(profile: "application", capability: "microservice");
        var preview = await service.InitAsync(request, cancellationToken: CancellationToken);
        // Changing instruction files always reports the session-reload warning.
        Assert.Equal(GuideStatus.Warning, preview.Status);
        Assert.False(File.Exists(fixture.ConfigFile));
        Assert.Contains(preview.Plan!.Actions, action =>
            action.Kind == GuidePlanActionKind.WriteFile && action.Target.EndsWith(Path.Join(".monica", "guide.json"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(preview.Plan.Actions, action =>
            action.Kind == GuidePlanActionKind.WriteFile && action.Target.EndsWith("AGENTS.md", StringComparison.OrdinalIgnoreCase));

        var applied = await service.InitAsync(request, preview.Plan.PlanDigest, cancellationToken: CancellationToken);
        Assert.True(applied.Plan!.Applied);

        var config = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(fixture.ConfigFile, CancellationToken));
        Assert.Equal(1, config.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("application", config.GetProperty("profile").GetString());
        Assert.Equal("Tairitsua.Monica", config.GetProperty("productId").GetString());

        var agents = await File.ReadAllTextAsync(fixture.WorkspaceFile("AGENTS.md"), CancellationToken);
        Assert.Contains(WorkspaceFixture.MarkerStart, agents, StringComparison.Ordinal);
        Assert.Contains("Profile skills: $monica-guide.", agents, StringComparison.Ordinal);
        Assert.Contains("- Use $monica-guide for toolbox help.", agents, StringComparison.Ordinal);

        // Re-initialization with the same inputs is a no-op.
        var repeat = await service.InitAsync(request, null, cancellationToken: CancellationToken);
        Assert.True(repeat.Plan!.IsNoOp);
    }

    [Fact]
    public async Task Init_RequiresExplicitProfileAndCapabilities()
    {
        using var fixture = new WorkspaceFixture();
        var service = fixture.CreateService();
        fixture.WriteProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Monica.Core" Version="1.2.3" />
              </ItemGroup>
            </Project>
            """);

        var withoutProfile = await service.InitAsync(fixture.InitRequest(), cancellationToken: CancellationToken);
        Assert.Equal(GuideStatus.Error, withoutProfile.Status);
        Assert.Contains(withoutProfile.Checks, check => check.Id == "workspace.profile");

        var withoutArchitecture = await service.InitAsync(
            fixture.InitRequest(profile: "application"), cancellationToken: CancellationToken);
        Assert.Equal(GuideStatus.Error, withoutArchitecture.Status);
        Assert.Contains(withoutArchitecture.Checks, check => check.Id == "workspace.capabilities");

        var conflicting = await service.InitAsync(
            fixture.InitRequest(profile: "application", capability: "microservice", second: "modular-monolith"),
            cancellationToken: CancellationToken);
        Assert.Contains(conflicting.Checks, check =>
            check.Id == "workspace.capabilities" && check.Status == GuideCheckStatus.Error);
    }

    [Fact]
    public async Task Init_RejectsExtensionProfileWithMonicaProjectReference()
    {
        using var fixture = new WorkspaceFixture();
        var service = fixture.CreateService();
        fixture.WriteProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\\..\\Monica\\Monica.Core\\Monica.Core.csproj" />
              </ItemGroup>
            </Project>
            """);

        var preview = await service.InitAsync(
            fixture.InitRequest(profile: "extension-author"), cancellationToken: CancellationToken);

        Assert.Equal(GuideStatus.Error, preview.Status);
        Assert.Contains(preview.Checks, check => check.Id == "workspace.project-reference");
    }

    [Fact]
    public async Task Init_ProjectsBoundSourcesAndTheIssuePolicyIntoTheManagedBlock()
    {
        using var fixture = new WorkspaceFixture();
        var root = Path.GetDirectoryName(fixture.Workspace)!;
        var checkout = Path.Combine(root, "checkout");
        Directory.CreateDirectory(checkout);
        var bindRequest = new GuideSourceBindRequest("monica", checkout, null);
        var source = new GuideSourceService(fixture.EnginePaths, null, new CanonicalCheckoutProbe(checkout));
        var bind = await source.BindAsync(bindRequest, cancellationToken: CancellationToken);
        await source.BindAsync(bindRequest, bind.Plan!.PlanDigest, cancellationToken: CancellationToken);

        var service = fixture.CreateService();
        fixture.WriteProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Monica.Core" Version="1.2.3" />
              </ItemGroup>
            </Project>
            """);
        var request = fixture.InitRequest(profile: "application", capability: "microservice");
        var preview = await service.InitAsync(request, cancellationToken: CancellationToken);
        await service.InitAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);

        var agents = await File.ReadAllTextAsync(fixture.WorkspaceFile("AGENTS.md"), CancellationToken);
        Assert.Contains("## First-party source on this machine", agents, StringComparison.Ordinal);
        Assert.Contains(checkout, agents, StringComparison.Ordinal);
        Assert.Contains("lookup only, never write permission", agents, StringComparison.Ordinal);
        Assert.Contains("(commit 0123456789ab).", agents, StringComparison.Ordinal);
        Assert.Contains("## Issue reporting policy", agents, StringComparison.Ordinal);
        Assert.Contains("Machine policy is 'prepare'", agents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Init_OmitsProjectionSectionsWhenTheSwitchesAreOff()
    {
        using var fixture = new WorkspaceFixture();
        GuideWorkspaceProjectionStore.Save(
            fixture.EnginePaths,
            new GuideWorkspaceProjection(GuideWorkspaceProjection.CurrentSchemaVersion, SourceHints: false, IssuePolicy: false));
        var service = fixture.CreateService();
        fixture.WriteProject(string.Empty);
        var request = fixture.InitRequest(profile: "application", capability: "microservice");
        var preview = await service.InitAsync(request, cancellationToken: CancellationToken);
        await service.InitAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);

        var agents = await File.ReadAllTextAsync(fixture.WorkspaceFile("AGENTS.md"), CancellationToken);
        Assert.Contains("Profile skills: $monica-guide.", agents, StringComparison.Ordinal);
        Assert.DoesNotContain("First-party source on this machine", agents, StringComparison.Ordinal);
        Assert.DoesNotContain("Issue reporting policy", agents, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewManagedInstructions_RendersMarkersTemplatesAndMachineSections()
    {
        using var fixture = new WorkspaceFixture();
        var service = fixture.CreateService();

        var preview = service.PreviewManagedInstructions("application");

        Assert.Equal("application", preview.Profile);
        Assert.Equal(WorkspaceFixture.MarkerStart, preview.StartMarker);
        Assert.Equal(WorkspaceFixture.MarkerEnd, preview.EndMarker);
        Assert.Contains("Profile skills: $monica-guide.", preview.Body, StringComparison.Ordinal);
        // Nothing is bound on this machine, so the source-hint switch projects no section.
        Assert.DoesNotContain("First-party source on this machine", preview.Body, StringComparison.Ordinal);
        Assert.Contains("## Issue reporting policy", preview.Body, StringComparison.Ordinal);
        Assert.Contains("Machine policy is 'prepare'", preview.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewManagedInstructions_ResolvesNullOrUnknownProfileToTheFirstTemplate()
    {
        using var fixture = new WorkspaceFixture();
        var service = fixture.CreateService();

        Assert.Equal("application", service.PreviewManagedInstructions().Profile);
        Assert.Equal("application", service.PreviewManagedInstructions("not-a-profile").Profile);
    }

    [Fact]
    public async Task PreviewManagedInstructions_ReflectsBoundSourcesSwitchesAndMode()
    {
        using var fixture = new WorkspaceFixture();
        var root = Path.GetDirectoryName(fixture.Workspace)!;
        var checkout = Path.Combine(root, "checkout");
        Directory.CreateDirectory(checkout);
        var bindRequest = new GuideSourceBindRequest("monica", checkout, null);
        var source = new GuideSourceService(fixture.EnginePaths, null, new CanonicalCheckoutProbe(checkout));
        var bind = await source.BindAsync(bindRequest, cancellationToken: CancellationToken);
        await source.BindAsync(bindRequest, bind.Plan!.PlanDigest, cancellationToken: CancellationToken);
        GuideIssuePreferencesStore.Save(
            fixture.EnginePaths,
            new GuideIssuePreferences(GuideIssuePreferences.CurrentSchemaVersion, GuideIssueReportingMode.Never));

        var preview = fixture.CreateService().PreviewManagedInstructions();

        Assert.Contains("## First-party source on this machine", preview.Body, StringComparison.Ordinal);
        Assert.Contains(checkout, preview.Body, StringComparison.Ordinal);
        Assert.Contains("Machine policy is 'never'", preview.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewManagedInstructions_SkipsMachineSectionsForProductsThatDoNotOwnThePolicy()
    {
        using var fixture = new WorkspaceFixture();
        var root = Path.GetDirectoryName(fixture.Workspace)!;
        var checkout = Path.Combine(root, "checkout");
        Directory.CreateDirectory(checkout);
        var bindRequest = new GuideSourceBindRequest("monica", checkout, null);
        var source = new GuideSourceService(fixture.EnginePaths, null, new CanonicalCheckoutProbe(checkout));
        var bind = await source.BindAsync(bindRequest, cancellationToken: CancellationToken);
        await source.BindAsync(bindRequest, bind.Plan!.PlanDigest, cancellationToken: CancellationToken);
        GuideIssuePreferencesStore.Save(
            fixture.EnginePaths,
            new GuideIssuePreferences(GuideIssuePreferences.CurrentSchemaVersion, GuideIssueReportingMode.Never));

        var preview = fixture.CreateService(product: KnownAgentProducts.MonicaWorkflow).PreviewManagedInstructions();

        Assert.Contains("Profile skills: $monica-guide.", preview.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("First-party source on this machine", preview.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Issue reporting policy", preview.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewManagedInstructions_OmitsBothSectionsWhenTheSwitchesAreOff()
    {
        using var fixture = new WorkspaceFixture();
        GuideWorkspaceProjectionStore.Save(
            fixture.EnginePaths,
            new GuideWorkspaceProjection(GuideWorkspaceProjection.CurrentSchemaVersion, SourceHints: false, IssuePolicy: false));

        var preview = fixture.CreateService().PreviewManagedInstructions();

        Assert.Contains("Profile skills: $monica-guide.", preview.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("First-party source on this machine", preview.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Issue reporting policy", preview.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Init_RejectsStalePlanDigestAndFileDrift()
    {
        using var fixture = new WorkspaceFixture();
        var service = fixture.CreateService();
        fixture.WriteProject(string.Empty);
        var request = fixture.InitRequest(profile: "application", capability: "microservice");

        var stale = await service.InitAsync(request, new string('a', 64), cancellationToken: CancellationToken);
        Assert.Equal(GuideStatus.Error, stale.Status);
        Assert.Contains(stale.Checks, check => check.Id == "plan.digest");

        var preview = await service.InitAsync(request, null, cancellationToken: CancellationToken);
        await File.WriteAllTextAsync(fixture.WorkspaceFile("AGENTS.md"), "changed after preview", CancellationToken);
        var drifted = await service.InitAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);
        Assert.Equal(GuideStatus.Error, drifted.Status);
        // Recomputing the plan under the lock folds any post-preview file change into the
        // digest gate; no separate drift path is needed.
        Assert.Contains(drifted.Checks, check => check.Id == "plan.digest");
    }

    [Fact]
    public async Task Init_WarnsOnLegacyConfigurationAndRewritesIt()
    {
        using var fixture = new WorkspaceFixture();
        var service = fixture.CreateService();
        fixture.WriteProject(string.Empty);
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.ConfigFile)!);
        await File.WriteAllTextAsync(
            fixture.ConfigFile,
            """{"schemaVersion":2,"profile":"application","channel":"stable"}""",
            CancellationToken);

        var preview = await service.InitAsync(
            fixture.InitRequest(profile: "application", capability: "microservice"),
            cancellationToken: CancellationToken);

        Assert.Contains(preview.Checks, check =>
            check.Id == "workspace.config" && check.Status == GuideCheckStatus.Warning);
        var applied = await service.InitAsync(
            fixture.InitRequest(profile: "application", capability: "microservice"),
            preview.Plan!.PlanDigest,
            cancellationToken: CancellationToken);
        Assert.True(applied.Plan!.Applied);
        var config = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(fixture.ConfigFile, CancellationToken));
        Assert.Equal(1, config.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task Init_ManagesClaudeImportOnlyWhenClaudeTargetIsInstalled()
    {
        using var fixture = new WorkspaceFixture();
        fixture.WriteClaudeInstallation();
        var service = fixture.CreateService();
        fixture.WriteProject(string.Empty);
        var request = fixture.InitRequest(profile: "application", capability: "microservice");

        var preview = await service.InitAsync(request, cancellationToken: CancellationToken);
        await service.InitAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);

        var claude = await File.ReadAllTextAsync(fixture.WorkspaceFile("CLAUDE.md"), CancellationToken);
        Assert.Contains("@AGENTS.md", claude, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forget_RemovesEveryGuideOwnedArtifact()
    {
        using var fixture = new WorkspaceFixture();
        var service = fixture.CreateService();
        fixture.WriteProject(string.Empty);
        var request = fixture.InitRequest(profile: "application", capability: "microservice");
        var preview = await service.InitAsync(request, cancellationToken: CancellationToken);
        await service.InitAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);
        await File.WriteAllTextAsync(
            fixture.WorkspaceFile("AGENTS.md"),
            $"# Kept\n\n{WorkspaceFixture.MarkerStart}\nmanaged\n{WorkspaceFixture.MarkerEnd}\n\nKept trailer.\n",
            CancellationToken);
        await File.WriteAllTextAsync(fixture.WorkspaceFile("CLAUDE.md"), "Intro\n\n@AGENTS.md\n", CancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.ConfigFile)!);
        await File.WriteAllTextAsync(
            fixture.ConfigFile,
            $$"""{"schemaVersion":1,"productId":"Tairitsua.Monica","profile":"application","capabilities":[],"instructionBlockVersion":2,"managedClaudeImport":true}""",
            CancellationToken);

        var forgetPreview = await service.ForgetAsync(fixture.Workspace, cancellationToken: CancellationToken);
        Assert.Equal(GuideStatus.Ready, forgetPreview.Status);
        var forgotten = await service.ForgetAsync(fixture.Workspace, forgetPreview.Plan!.PlanDigest, cancellationToken: CancellationToken);
        Assert.True(forgotten.Plan!.Applied);

        var agents = await File.ReadAllTextAsync(fixture.WorkspaceFile("AGENTS.md"), CancellationToken);
        Assert.DoesNotContain(WorkspaceFixture.MarkerStart, agents, StringComparison.Ordinal);
        Assert.Contains("# Kept", agents, StringComparison.Ordinal);
        var claude = await File.ReadAllTextAsync(fixture.WorkspaceFile("CLAUDE.md"), CancellationToken);
        Assert.DoesNotContain("@AGENTS.md", claude, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.ConfigFile));
    }

    [Fact]
    public void Inspect_ReportsConfiguredWorkspaceHealth()
    {
        using var fixture = new WorkspaceFixture();
        fixture.WriteProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Monica.Core" Version="1.2.3" />
              </ItemGroup>
            </Project>
            """);
        var service = fixture.CreateService();

        var report = service.Inspect(fixture.Workspace);

        Assert.Contains(report.Checks, check =>
            check.Id == "workspace.repository" && check.Message.Contains("Monica package", StringComparison.Ordinal));
        Assert.Contains(report.Checks, check =>
            check.Id == "workspace.profile" && check.Status == GuideCheckStatus.Warning);
        Assert.Contains(report.Checks, check =>
            check.Id == "workspace.framework" && check.Status == GuideCheckStatus.Ok
            && check.Message.Contains("1.2.3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Inspect_DetectsMissingInstructionBlockForConfiguredWorkspace()
    {
        using var fixture = new WorkspaceFixture();
        var service = fixture.CreateService();
        fixture.WriteProject(string.Empty);
        var request = fixture.InitRequest(profile: "application", capability: "microservice");
        var preview = await service.InitAsync(request, cancellationToken: CancellationToken);
        await service.InitAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);
        await File.WriteAllTextAsync(fixture.WorkspaceFile("AGENTS.md"), "rewritten\n", CancellationToken);

        var report = service.Inspect(fixture.Workspace);

        Assert.Contains(report.Checks, check =>
            check.Id == "workspace.instructions" && check.Status == GuideCheckStatus.Error);
    }

    [Fact]
    public void InstructionBlock_UpholdsMarkerContracts()
    {
        var markers = WorkspaceFixture.Markers;
        const string body = "## Monica agent workflow\n\nProfile skills: $monica-guide.";
        var inserted = GuideInstructionBlock.Upsert($"# Title\n\nTrailer.\n", body, markers);
        Assert.Equal(
            $"# Title\n\nTrailer.\n\n{WorkspaceFixture.MarkerStart}\n{body}\n{WorkspaceFixture.MarkerEnd}\n",
            inserted.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(GuideInstructionBlockStatus.Valid, GuideInstructionBlock.State(inserted, markers));

        var replaced = GuideInstructionBlock.Upsert(inserted, "Updated body.", markers);
        Assert.Equal(
            $"# Title\n\nTrailer.\n\n{WorkspaceFixture.MarkerStart}\nUpdated body.\n{WorkspaceFixture.MarkerEnd}\n",
            replaced.Replace("\r\n", "\n", StringComparison.Ordinal));

        var removed = GuideInstructionBlock.Remove(replaced, markers);
        // Removal splices the span out verbatim, exactly like the retired Node engine; the
        // separator blank lines remain for the next upsert.
        Assert.Equal("# Title\n\nTrailer.\n\n\n", removed.Replace("\r\n", "\n", StringComparison.Ordinal));

        Assert.Equal(GuideInstructionBlockStatus.Malformed,
            GuideInstructionBlock.State($"{WorkspaceFixture.MarkerStart}\nbody\n", markers));
        Assert.Equal(GuideInstructionBlockStatus.Absent, GuideInstructionBlock.State("plain text\n", markers));
    }

    [Fact]
    public void ClaudeImport_RoundTripsAndRejectsDuplicates()
    {
        Assert.Equal(GuideClaudeImportStatus.Absent, GuideInstructionBlock.ClaudeImportState("Intro\n"));
        var ensured = GuideInstructionBlock.EnsureClaudeImport("Intro\n");
        Assert.Equal("Intro\n\n@AGENTS.md\n", ensured.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(GuideClaudeImportStatus.Valid, GuideInstructionBlock.ClaudeImportState(ensured));
        Assert.Equal("Intro\n\n", GuideInstructionBlock.RemoveClaudeImport(ensured)
            .Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(GuideClaudeImportStatus.Duplicate,
            GuideInstructionBlock.ClaudeImportState("@AGENTS.md\n@AGENTS.md\n"));
        Assert.Throws<InvalidOperationException>(() => GuideInstructionBlock.EnsureClaudeImport("@AGENTS.md\n@AGENTS.md\n"));
    }

    [Fact]
    public void DetectCandidate_ReportsStableOutcomeCodes()
    {
        using var fixture = new WorkspaceFixture();
        var service = fixture.CreateService();
        fixture.WriteProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Monica.Core" Version="1.2.3" />
              </ItemGroup>
            </Project>
            """);

        var application = service.DetectCandidate(fixture.Workspace);
        Assert.Equal(GuideWorkspaceDetectionOutcome.ApplicationCharacteristics, application.Outcome);
        Assert.Equal("application", application.CandidateProfile);
        Assert.False(application.Initialized);

        // Adding extension source characteristics turns the candidate ambiguous instead of
        // guessing; the outcome code is what the wizard localizes for explicit selection.
        File.WriteAllText(
            fixture.WorkspaceFile("ModuleOrders.cs"),
            "public sealed class ModuleOrders : ModuleRegistration<ModuleOrders> { }");
        var ambiguous = service.DetectCandidate(fixture.Workspace);
        Assert.Equal(GuideWorkspaceDetectionOutcome.AmbiguousApplicationAndExtension, ambiguous.Outcome);
        Assert.Null(ambiguous.CandidateProfile);
        Assert.Equal("ambiguous", ambiguous.Confidence);
    }

    [Fact]
    public void DetectCandidate_FlagsMissingDirectoryWithoutDetecting()
    {
        using var fixture = new WorkspaceFixture();
        var service = fixture.CreateService();
        var missing = Path.Combine(Path.GetTempPath(), $"guide-missing-{Guid.NewGuid():N}");

        var candidate = service.DetectCandidate(missing);

        Assert.Equal(GuideWorkspaceDetectionOutcome.NotADirectory, candidate.Outcome);
        Assert.Null(candidate.CandidateProfile);
    }

    [Fact]
    public void DetectCandidate_TrustsCanonicalRemoteIdentityWithoutStatusScan()
    {
        using var fixture = new WorkspaceFixture();
        var service = fixture.CreateService(new RemoteGitProbe(fixture.Workspace, "https://github.com/Tairitsua/Monica.git"));

        var candidate = service.DetectCandidate(fixture.Workspace);

        Assert.Equal(GuideWorkspaceDetectionOutcome.FrameworkRepository, candidate.Outcome);
        Assert.Equal("framework-contributor", candidate.CandidateProfile);
        Assert.Equal("canonical", candidate.Confidence);
    }

    private sealed class WorkspaceFixture : IDisposable
    {
        internal const string MarkerStart = "<!-- monica-guide:managed:start -->";
        internal const string MarkerEnd = "<!-- monica-guide:managed:end -->";
        private const string SkillName = "monica-guide";
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"guide-workspace-{Guid.NewGuid():N}");

        internal WorkspaceFixture()
        {
            EnginePaths = new GuidePaths(Path.Combine(_root, "engine-data"));
            ProductPaths = new AgentProductPaths(Path.Combine(_root, "product-data"));
            Workspace = Path.Combine(_root, "workspace");
            SkillsRoot = Path.Combine(_root, "bundle", "skills");
            Directory.CreateDirectory(Workspace);
            WriteCatalog();
        }

        internal static AgentProductDefinition Product => KnownAgentProducts.Monica;
        internal static GuideInstructionMarkers Markers { get; } = new(MarkerStart, MarkerEnd);
        internal GuidePaths EnginePaths { get; }
        internal AgentProductPaths ProductPaths { get; }
        internal string Workspace { get; }
        internal string SkillsRoot { get; }
        internal string ConfigFile => Path.Combine(Workspace, ".monica", "guide.json");

        internal GuideWorkspaceService CreateService(IGuideGitProbe? git = null, AgentProductDefinition? product = null)
            => new(product ?? Product, EnginePaths, LoadCatalog(), ProductPaths, git ?? new NoGitProbe());

        internal GuideWorkspaceInitRequest InitRequest(string? profile = null, string? capability = null, string? second = null)
        {
            var capabilities = new List<string>();
            if (capability is not null) capabilities.Add(capability);
            if (second is not null) capabilities.Add(second);
            return new GuideWorkspaceInitRequest(Workspace, profile, capabilities);
        }

        internal string WorkspaceFile(string name) => Path.Combine(Workspace, name);

        internal void WriteProject(string projectText)
        {
            Directory.CreateDirectory(Path.Combine(Workspace, "src", "App"));
            if (projectText.Length > 0)
            {
                File.WriteAllText(Path.Combine(Workspace, "src", "App", "App.csproj"), projectText);
            }
        }

        /// <summary>Records one Claude target installation in the unified ledger.</summary>
        internal void WriteClaudeInstallation()
        {
            var targetRoot = Path.Combine(_root, "claude-skills");
            var state = new ProductGuideState(
                Product.ProductId,
                "1.0.0",
                Path.Combine(_root, "app", "Monica.Guide.exe"),
                Path.Combine(_root, "bundle"),
                "digest",
                null,
                [new GuideSkillInstallation(
                    new GuideEnvironment("windows", "windows"),
                    GuideTarget.Claude,
                    targetRoot,
                    null,
                    [])]);
            var ledger = new GuideLedger(
                GuideLedger.CurrentSchemaVersion,
                [state]);
            Directory.CreateDirectory(EnginePaths.StateDirectory);
            File.WriteAllText(
                EnginePaths.GuideLedgerFile,
                JsonSerializer.Serialize(ledger, GuidePlanning.JsonOptions));
        }

        private SkillCatalog LoadCatalog()
            => GuideReleaseMetadata.LoadSkillCatalog(SkillsRoot);

        private void WriteCatalog()
        {
            var skillRoot = Path.Combine(SkillsRoot, SkillName);
            Directory.CreateDirectory(skillRoot);
            var content = Encoding.UTF8.GetBytes("---\nname: monica-guide\ndescription: test\n---\n");
            File.WriteAllBytes(Path.Combine(skillRoot, "SKILL.md"), content);
            var treeDigest = SkillTreeDigest(("SKILL.md", content));
            var catalog = new
            {
                schemaVersion = 1,
                skillCount = 1,
                treeDigest = CatalogDigest((SkillName, treeDigest)),
                skills = new object[]
                {
                    new
                    {
                        name = SkillName,
                        path = $"skills/{SkillName}",
                        role = "guide",
                        files = new[] { "SKILL.md" },
                        dependencies = Array.Empty<string>(),
                        treeDigest
                    }
                },
                managedInstructions = new
                {
                    version = 2,
                    markers = new { start = MarkerStart, end = MarkerEnd },
                    templates = new Dictionary<string, object>
                    {
                        ["application"] = new
                        {
                            skills = new[] { SkillName },
                            rules = new[] { "Use $monica-guide for toolbox help." }
                        },
                        ["extension-author"] = new
                        {
                            skills = new[] { SkillName },
                            rules = new[] { "Use $monica-guide for toolbox help." }
                        },
                        ["framework-contributor"] = new
                        {
                            skills = new[] { SkillName },
                            rules = new[] { "Use $monica-guide for toolbox help." }
                        }
                    }
                },
                sourceRepositories = new Dictionary<string, object>
                {
                    ["Tairitsua/Monica"] = new { repository = "Tairitsua/Monica", aliases = new[] { "monica" } }
                }
            };
            File.WriteAllText(Path.Combine(SkillsRoot, "catalog.json"), JsonSerializer.Serialize(catalog, GuidePlanning.JsonOptions));
        }

        /// <summary>Engine-scheme per-skill digest: SHA256 over sorted relativePath \0 content \0 records.</summary>
        private static string SkillTreeDigest(params (string Path, byte[] Content)[] files)
        {
            using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
            foreach (var (path, content) in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
            {
                var pathBytes = Encoding.UTF8.GetBytes(path);
                hash.AppendData(pathBytes);
                hash.AppendData([0]);
                hash.AppendData(content);
                hash.AppendData([0]);
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        /// <summary>Engine-scheme catalog aggregate digest: SHA256 over name \0 treeDigest \0 records.</summary>
        private static string CatalogDigest(params (string Name, string TreeDigest)[] skills)
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

    /// <summary>Non-git workspaces: detection stays profile-ambiguous and never throws.</summary>
    private sealed class NoGitProbe : IGuideGitProbe
    {
        public GuideGitInfo? Describe(string path) => null;

        public GuideGitIdentity? FindIdentity(string path) => null;

        public string? ResolveTagCommit(string repositoryRoot, string tag) => null;
    }

    /// <summary>
    /// Canonical remote identity for one path. <see cref="IGuideGitProbe.Describe"/> throws
    /// because detection must never pay for the commit and worktree-status scan it does.
    /// </summary>
    private sealed class RemoteGitProbe(string root, string remote) : IGuideGitProbe
    {
        public GuideGitInfo? Describe(string path) => throw new NotSupportedException("Detection must use the identity probe.");

        public GuideGitIdentity? FindIdentity(string path)
            => string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
                ? new GuideGitIdentity(root, GuideGitProbe.CanonicalRepository(remote))
                : null;

        public string? ResolveTagCommit(string repositoryRoot, string tag) => null;
    }
}
