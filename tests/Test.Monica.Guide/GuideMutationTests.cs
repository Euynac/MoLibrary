using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Monica.Guide;
using Xunit;

namespace Test.Monica.Guide;

public sealed class WorkflowGuideMutationTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Configure_ReplacesSkillDirectoriesAndWritesState()
    {
        using var fixture = new GuideFixture();
        using var service = fixture.CreateService();
        var request = fixture.ConfigureRequest(62031);

        var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
        Assert.False(preview.Plan!.Applied);
        Assert.Contains(preview.Plan.Actions, action =>
            action.Kind == GuidePlanActionKind.ReplaceSkillDirectory
            && action.Target.EndsWith("monica-workflow-guide", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(fixture.LedgerFile));

        var applied = await service.ApplyConfigureAsync(request, preview.Plan.PlanDigest, cancellationToken: CancellationToken);
        Assert.True(applied.Plan!.Applied);
        Assert.True(File.Exists(Path.Combine(fixture.TargetSharedSkillDirectory, "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(fixture.TargetSharedSkillDirectory, "references", "help.md")));
        Assert.True(File.Exists(fixture.LedgerFile));
        Assert.True(File.Exists(fixture.ProductPaths.InstallationLocatorFile));
        Assert.Equal(62031, await fixture.ReadServerConfigurationPortAsync());

        // Wholesale replacement is idempotent: a repeated apply lands the same bytes.
        var repeated = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
        var reapplied = await service.ApplyConfigureAsync(request, repeated.Plan!.PlanDigest, cancellationToken: CancellationToken);
        Assert.True(reapplied.Plan!.Applied);
        Assert.Equal(
            GuideFixture.SkillFileContent,
            await File.ReadAllTextAsync(Path.Combine(fixture.TargetSharedSkillDirectory, "SKILL.md"), CancellationToken));
    }

    [Fact]
    public async Task Configure_WithoutSelectionsRefreshesRecordedInstallations()
    {
        using var fixture = new GuideFixture();
        fixture.WriteCatalog(withObsoleteSkill: true);
        using var service = fixture.CreateService();
        var install = fixture.ConfigureRequest(62032);
        var installPreview = await service.PreviewConfigureAsync(install, cancellationToken: CancellationToken);
        await service.ApplyConfigureAsync(install, installPreview.Plan!.PlanDigest, cancellationToken: CancellationToken);
        Assert.True(Directory.Exists(fixture.TargetSharedObsoleteSkillDirectory));

        // A shrunken catalog refreshing the recorded installation removes the obsolete
        // skill directory wholesale.
        fixture.WriteCatalog();
        var refresh = new GuideConfigureRequest(null, new Uri("http://localhost:62032"), []);
        var refreshPreview = await service.PreviewConfigureAsync(refresh, cancellationToken: CancellationToken);
        var refreshed = await service.ApplyConfigureAsync(refresh, refreshPreview.Plan!.PlanDigest, cancellationToken: CancellationToken);

        Assert.True(refreshed.Plan!.Applied);
        Assert.False(Directory.Exists(fixture.TargetSharedObsoleteSkillDirectory));
        Assert.True(Directory.Exists(fixture.TargetSharedSkillDirectory));
    }

    [Fact]
    public async Task Configure_WithoutSelectionsOrRecordedStateFails()
    {
        using var fixture = new GuideFixture();
        using var service = fixture.CreateService();
        var request = new GuideConfigureRequest(null, new Uri("http://localhost:62033"), []);

        var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);

        Assert.Equal(GuideStatus.Error, preview.Status);
        Assert.Contains(preview.Checks, check => check.Id == "configuration.empty");
        Assert.False(File.Exists(fixture.LedgerFile));
    }

    [Fact]
    public async Task Configure_SharedTargetDoesNotRequireAnyDetectedHost()
    {
        using var fixture = new GuideFixture();
        fixture.Runtime.NoCommands = true;
        using var service = fixture.CreateService();
        var request = fixture.ConfigureRequest(62034);

        var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
        var applied = await service.ApplyConfigureAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);

        Assert.True(applied.Plan!.Applied);
        Assert.True(Directory.Exists(fixture.TargetSharedSkillDirectory));
    }

    [Fact]
    public async Task Configure_RejectsASameNameSkillEntryThatIsNotADirectory()
    {
        using var fixture = new GuideFixture();
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.TargetSharedSkillDirectory)!);
        await File.WriteAllTextAsync(
            fixture.TargetSharedSkillDirectory,
            "unowned non-directory entry",
            cancellationToken: CancellationToken);
        using var service = fixture.CreateService();

        var preview = await service.PreviewConfigureAsync(
            fixture.ConfigureRequest(62035),
            cancellationToken: CancellationToken);

        Assert.Equal(GuideStatus.Error, preview.Status);
        Assert.Contains(preview.Checks, check =>
            check.Id.Contains("target-kind", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Configure_RejectsRedirectedTargetRoot()
    {
        using var fixture = new GuideFixture();
        fixture.Runtime.RedirectedPaths.Add(fixture.TargetSharedRoot);
        using var service = fixture.CreateService();

        var preview = await service.PreviewConfigureAsync(
            fixture.ConfigureRequest(62036),
            cancellationToken: CancellationToken);

        Assert.Equal(GuideStatus.Error, preview.Status);
        Assert.Contains(preview.Checks, check =>
            check.Id.Contains("target-redirection", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Configure_RevalidatesDigestAndRefusesDrift()
    {
        using var fixture = new GuideFixture();
        using var service = fixture.CreateService();
        var request = fixture.ConfigureRequest(62037);

        var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
        // Changing a catalog source file after preview changes the rebuilt plan's digest.
        await File.WriteAllTextAsync(
            Path.Combine(fixture.SkillsRoot, GuideFixture.SkillBundleDirectory, "SKILL.md"),
            "drifted source content",
            cancellationToken: CancellationToken);
        var refused = await service.ApplyConfigureAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);

        Assert.Equal(GuideStatus.Error, refused.Status);
        Assert.Contains(refused.Checks, check => check.Id == "plan.digest");
        Assert.False(File.Exists(fixture.LedgerFile));
    }

    [Fact]
    public async Task Configure_RefusesApplyWhileAnotherMutationOwnsTheLock()
    {
        using var fixture = new GuideFixture();
        Directory.CreateDirectory(fixture.EnginePaths.StateDirectory);
        using var held = new FileStream(
            fixture.EnginePaths.GuideLockFile,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            1,
            FileOptions.Asynchronous);
        using var service = fixture.CreateService();
        var request = fixture.ConfigureRequest(62038);
        var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApplyConfigureAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken));
        held.Dispose();
    }

    [Fact]
    public async Task Diagnose_AndApply_StreamPhaseProgress()
    {
        using var fixture = new GuideFixture();
        using var service = fixture.CreateService();
        var request = fixture.ConfigureRequest(62039);
        var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);

        var applyPhases = new List<GuidePhase>();
        var applied = await service.ApplyConfigureAsync(
            request,
            preview.Plan!.PlanDigest,
            new InlineProgress(applyPhases.Add),
            cancellationToken: CancellationToken);
        Assert.True(applied.Plan!.Applied);
        Assert.Contains(applyPhases, phase => phase.Key == "apply.step" && phase.Total > 0 && phase.Completed == phase.Total);
        Assert.Contains(applyPhases, phase => phase.Key == "apply.verify");

        var diagnosePhases = new List<GuidePhase>();
        var health = await service.DiagnoseAsync(
            null,
            null,
            new InlineProgress(diagnosePhases.Add),
            cancellationToken: CancellationToken);
        // The loopback runtime is not serving in this fixture, so probe checks fail; the
        // phase stream itself is what this test pins down.
        Assert.Contains(health.Checks, check => check.Id == "runtime.healthz");
        Assert.Equal("status.state", diagnosePhases[0].Key);
        Assert.Contains(diagnosePhases, phase => phase.Key.StartsWith("status.skills.", StringComparison.Ordinal));
        Assert.Contains(diagnosePhases, phase => phase.Key == "runtime.healthz");
    }

    [Fact]
    public async Task StatusVerifiesInstalledTreesAndDetectsDrift()
    {
        using var fixture = new GuideFixture();
        using var service = fixture.CreateService();
        var request = fixture.ConfigureRequest(62041);
        var preview = await service.PreviewConfigureAsync(request, cancellationToken: CancellationToken);
        await service.ApplyConfigureAsync(request, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);

        var healthy = await service.GetStatusAsync(cancellationToken: CancellationToken);
        Assert.Contains(healthy.Checks, check =>
            check.Id == "skills.windows.shared.catalog"
            && check.Status == GuideCheckStatus.Ok);
        Assert.Contains(healthy.Checks, check =>
            check.Id.StartsWith("agent.", StringComparison.Ordinal)
            && check.Id.EndsWith(".detected", StringComparison.Ordinal));

        await File.WriteAllTextAsync(
            Path.Combine(fixture.TargetSharedSkillDirectory, "SKILL.md"),
            "tampered",
            cancellationToken: CancellationToken);
        var drifted = await service.GetStatusAsync(cancellationToken: CancellationToken);
        Assert.Contains(drifted.Checks, check =>
            check.Id == "skills.windows.shared.catalog"
            && check.Status == GuideCheckStatus.Warning);
    }

    [Fact]
    public async Task Unconfigure_RemovesSelectedTargetsAndRetainsOthers()
    {
        using var fixture = new GuideFixture();
        using var service = fixture.CreateService();
        var install = new GuideConfigureRequest(
            null,
            new Uri("http://localhost:62044"),
            [new GuideTargetSelection(FakeRuntime.WindowsEnvironment, [GuideTarget.Shared, GuideTarget.Claude])]);
        var preview = await service.PreviewConfigureAsync(install, cancellationToken: CancellationToken);
        await service.ApplyConfigureAsync(install, preview.Plan!.PlanDigest, cancellationToken: CancellationToken);
        Assert.True(Directory.Exists(fixture.TargetSharedSkillDirectory));
        Assert.True(Directory.Exists(fixture.TargetClaudeSkillDirectory));

        var claudeRemoval = new GuideUnconfigureRequest(
            [GuideTarget.Claude],
            [FakeRuntime.WindowsEnvironment]);
        var claudePreview = await service.PreviewUnconfigureAsync(claudeRemoval, cancellationToken: CancellationToken);
        Assert.Contains(claudePreview.Plan!.Actions, action =>
            action.Kind == GuidePlanActionKind.DeleteSkillDirectory);
        var claudeApplied = await service.ApplyUnconfigureAsync(claudeRemoval, claudePreview.Plan.PlanDigest, cancellationToken: CancellationToken);

        Assert.True(claudeApplied.Plan!.Applied);
        Assert.False(Directory.Exists(fixture.TargetClaudeSkillDirectory));
        Assert.True(Directory.Exists(fixture.TargetSharedSkillDirectory));
        Assert.True(File.Exists(fixture.LedgerFile));

        var fullRemoval = new GuideUnconfigureRequest();
        var fullPreview = await service.PreviewUnconfigureAsync(fullRemoval, cancellationToken: CancellationToken);
        var fullApplied = await service.ApplyUnconfigureAsync(fullRemoval, fullPreview.Plan!.PlanDigest, cancellationToken: CancellationToken);

        Assert.True(fullApplied.Plan!.Applied);
        Assert.False(Directory.Exists(fixture.TargetSharedSkillDirectory));
        Assert.False(Directory.Exists(fixture.TargetSharedRoot));
        Assert.False(File.Exists(fixture.LedgerFile));
        Assert.False(File.Exists(fixture.ProductPaths.InstallationLocatorFile));
        Assert.False(File.Exists(fixture.ProductPaths.ServerConfigurationFile));
    }

    [Fact]
    public async Task Unconfigure_WithoutRecordedStateReportsAlreadyAbsent()
    {
        using var fixture = new GuideFixture();
        using var service = fixture.CreateService();

        var report = await service.PreviewUnconfigureAsync(
            new GuideUnconfigureRequest(),
            cancellationToken: CancellationToken);

        Assert.True(report.Plan!.IsNoOp);
        Assert.Contains(report.Checks, check =>
            check.Id == "configuration.state"
            && check.Status == GuideCheckStatus.Ok);
    }

    [Fact]
    public async Task StatusWithoutGuideStateReportsCorruptServerConfigurationAsStructuredError()
    {
        using var fixture = new GuideFixture();
        Directory.CreateDirectory(fixture.ProductPaths.ConfigurationDirectory);
        await File.WriteAllTextAsync(
            fixture.ProductPaths.ServerConfigurationFile,
            "{not-json",
            cancellationToken: CancellationToken);
        using var service = fixture.CreateService();

        var status = await service.GetStatusAsync(cancellationToken: CancellationToken);

        Assert.Equal(GuideStatus.Error, status.Status);
        Assert.Contains(status.Checks, check =>
            check.Id == "server.configuration"
            && check.Status == GuideCheckStatus.Error);
    }

    private sealed class InlineProgress(Action<GuidePhase> sink) : IProgress<GuidePhase>
    {
        public void Report(GuidePhase value) => sink(value);
    }

    private sealed class GuideFixture : IDisposable
    {
        private const string SkillName = "monica-workflow-guide";
        internal const string SkillBundleDirectory = "monica-workflow-guide";
        internal const string SkillFileContent = "---\nname: monica-workflow-guide\ndescription: test\n---\n";
        internal const string NestedSkillFileContent = "test reference\n";

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"workflow-guide-{Guid.NewGuid():N}");

        internal GuideFixture()
        {
            EnginePaths = new GuidePaths(Path.Combine(_root, "engine-data"));
            ProductPaths = new AgentProductPaths(Path.Combine(_root, "product-data"));
            BundleRoot = Path.Combine(_root, "bundle");
            ApplicationDirectory = Path.Combine(BundleRoot, "app");
            SkillsRoot = Path.Combine(BundleRoot, "skills");
            ExecutablePath = Path.Combine(ApplicationDirectory, "Monica.Workflow.exe");
            Directory.CreateDirectory(ApplicationDirectory);
            File.WriteAllText(ExecutablePath, "test executable marker");
            WriteCatalog();
            Runtime = new FakeRuntime(Path.Combine(_root, "user-home"));
        }

        /// <summary>The workflow product definition drives the fixture bundle: the entry
        /// executable name, serve port, and MCP doctor surface all come from the registry.</summary>
        internal static AgentProductDefinition Product => KnownAgentProducts.MonicaWorkflow;

        internal GuidePaths EnginePaths { get; }
        internal AgentProductPaths ProductPaths { get; }
        internal string LedgerFile => EnginePaths.GuideLedgerFile;
        internal FakeRuntime Runtime { get; }
        internal string BundleRoot { get; }
        internal string ApplicationDirectory { get; }
        internal string SkillsRoot { get; }
        internal string ExecutablePath { get; }
        internal string Root => _root;
        internal string TargetSharedRoot => Path.Combine(Runtime.Home, ".agents", "skills");
        internal string TargetSharedSkillDirectory => Path.Combine(TargetSharedRoot, SkillName);
        internal string TargetSharedObsoleteSkillDirectory => Path.Combine(TargetSharedRoot, "monica-workflow-knowledge-requirement");
        internal string TargetClaudeSkillDirectory => Path.Combine(Runtime.Home, ".claude", "skills", SkillName);

        internal AgentGuideService CreateService()
            // The deterministic port probe keeps plans hermetic: no real loopback
            // listener (including a running product) can influence test outcomes.
            => new(
                Product,
                EnginePaths,
                ProductPaths,
                Runtime,
                null,
                ApplicationDirectory,
                static (_, _) => Task.FromResult(false));

        internal GuideConfigureRequest ConfigureRequest(int port, params GuideTarget[] targets)
            => new(
                null,
                new Uri($"http://localhost:{port}"),
                [new GuideTargetSelection(
                    FakeRuntime.WindowsEnvironment,
                    targets.Length == 0 ? [GuideTarget.Shared] : targets)]);

        internal async Task<int> ReadServerConfigurationPortAsync()
        {
            using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(
                ProductPaths.ServerConfigurationFile,
                CancellationToken));
            return document.RootElement.GetProperty("port").GetInt32();
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        internal void WriteCatalog(bool withObsoleteSkill = false)
        {
            string[] relativePaths = ["SKILL.md", "references/help.md"];
            Array.Sort(relativePaths, StringComparer.Ordinal);
            var skillRoot = Path.Combine(SkillsRoot, SkillName);
            Directory.CreateDirectory(skillRoot);
            var content = Encoding.UTF8.GetBytes(SkillFileContent);
            var nestedContent = Encoding.UTF8.GetBytes(NestedSkillFileContent);
            File.WriteAllBytes(Path.Combine(skillRoot, "SKILL.md"), content);
            Directory.CreateDirectory(Path.Combine(skillRoot, "references"));
            File.WriteAllBytes(Path.Combine(skillRoot, "references", "help.md"), nestedContent);

            using var skillHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var relativePath in relativePaths)
            {
                Append(skillHash, relativePath);
                skillHash.AppendData([0]);
                skillHash.AppendData(relativePath == "SKILL.md" ? content : nestedContent);
                skillHash.AppendData([0]);
            }
            var skillDigest = Convert.ToHexString(skillHash.GetHashAndReset()).ToLowerInvariant();

            using var aggregateHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Append(aggregateHash, SkillName);
            aggregateHash.AppendData([0]);
            Append(aggregateHash, skillDigest);
            aggregateHash.AppendData([0]);

            var skills = new List<object>
            {
                new
                {
                    name = SkillName,
                    path = $"skills/{SkillName}",
                    role = "guide",
                    files = relativePaths,
                    dependencies = Array.Empty<string>(),
                    treeDigest = skillDigest
                }
            };
            if (withObsoleteSkill)
            {
                // A second catalog skill so a later catalog without it exercises wholesale
                // obsolete-directory removal through the recorded-installation refresh.
                const string obsoleteSkill = "monica-workflow-knowledge-requirement";
                string[] obsoletePaths = ["SKILL.md", "templates/requirement.en-US.md"];
                var obsoleteRoot = Path.Combine(SkillsRoot, obsoleteSkill);
                Directory.CreateDirectory(obsoleteRoot);
                var obsoleteContent = Encoding.UTF8.GetBytes(
                    "---\nname: monica-workflow-knowledge-requirement\ndescription: test\n---\n");
                File.WriteAllBytes(Path.Combine(obsoleteRoot, "SKILL.md"), obsoleteContent);
                Directory.CreateDirectory(Path.Combine(obsoleteRoot, "templates"));
                File.WriteAllBytes(
                    Path.Combine(obsoleteRoot, "templates", "requirement.en-US.md"),
                    obsoleteContent);
                using var obsoleteHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                foreach (var relativePath in obsoletePaths)
                {
                    Append(obsoleteHash, relativePath);
                    obsoleteHash.AppendData([0]);
                    obsoleteHash.AppendData(obsoleteContent);
                    obsoleteHash.AppendData([0]);
                }
                var obsoleteDigest = Convert.ToHexString(obsoleteHash.GetHashAndReset()).ToLowerInvariant();
                skills.Add(new
                {
                    name = obsoleteSkill,
                    path = $"skills/{obsoleteSkill}",
                    role = "document",
                    files = obsoletePaths,
                    dependencies = Array.Empty<string>(),
                    treeDigest = obsoleteDigest
                });
                Append(aggregateHash, obsoleteSkill);
                aggregateHash.AppendData([0]);
                Append(aggregateHash, obsoleteDigest);
                aggregateHash.AppendData([0]);
            }
            var treeDigest = Convert.ToHexString(aggregateHash.GetHashAndReset()).ToLowerInvariant();

            var catalog = new
            {
                schemaVersion = 1,
                skillCount = skills.Count,
                treeDigest,
                skills
            };
            File.WriteAllText(
                Path.Combine(SkillsRoot, "catalog.json"),
                JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static void Append(IncrementalHash hash, string value)
            => hash.AppendData(Encoding.UTF8.GetBytes(value));
    }


}
