using Monica.Guide;
using Xunit;

namespace Test.Monica.Guide;

public sealed class GuideAgentPresenceTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetStatusAsync_CanSkipHostDetection()
    {
        var root = CreateRoot();
        try
        {
            using var service = CreateService(root);

            var skipped = await service.GetStatusAsync(
                new GuideInspectRequest(DetectHosts: false), cancellationToken: CancellationToken);
            Assert.DoesNotContain(skipped.Checks, check => check.Id.StartsWith("agent.", StringComparison.Ordinal));

            var live = await service.GetStatusAsync(cancellationToken: CancellationToken);
            var selector = FakeRuntime.HostEnvironment.Selector;
            Assert.Contains(live.Checks, check => check.Id == $"agent.codex.{selector}.detected");
            Assert.Contains(live.Checks, check => check.Id == $"agent.claude.{selector}.detected");
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task DetectAgentPresenceAsync_PersistsTheSnapshotForLaterReaders()
    {
        var root = CreateRoot();
        try
        {
            var enginePaths = new GuidePaths(Path.Combine(root, "engine-data"));
            using var service = CreateService(root);

            var snapshot = await service.DetectAgentPresenceAsync(cancellationToken: CancellationToken);

            var selector = FakeRuntime.HostEnvironment.Selector;
            Assert.Equal(2, snapshot.Presence.Count);
            Assert.Contains(snapshot.Presence, presence =>
                presence.Agent == GuideAgent.Codex && presence.EnvironmentSelector == selector && presence.Version == "codex-cli 0.147.0");
            Assert.Contains(snapshot.Presence, presence =>
                presence.Agent == GuideAgent.Claude && presence.EnvironmentSelector == selector && presence.Version == "2.1.221 (Claude Code)");
            Assert.True(File.Exists(enginePaths.AgentPresenceFile));

            // A later reader (the next wizard run) observes exactly the persisted snapshot.
            var persisted = GuideAgentPresenceStore.Load(enginePaths);
            Assert.NotNull(persisted);
            Assert.Equal(snapshot.DetectedAt, persisted!.DetectedAt);
            Assert.Equal(snapshot.Presence, persisted.Presence);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void PresenceStore_TreatsAnUnreadableSnapshotAsAbsent()
    {
        var root = CreateRoot();
        try
        {
            var enginePaths = new GuidePaths(Path.Combine(root, "engine-data"));
            Directory.CreateDirectory(enginePaths.StateDirectory);
            File.WriteAllText(enginePaths.AgentPresenceFile, "not a snapshot");

            Assert.Null(GuideAgentPresenceStore.Load(enginePaths));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task DetectAgentPresenceAsync_CarriesEnumerationWarningsInsteadOfDroppingThemSilently()
    {
        var root = CreateRoot();
        try
        {
            var enginePaths = new GuidePaths(Path.Combine(root, "engine-data"));
            using var service = CreateService(root, new FakeRuntime(Path.Combine(root, "user-home"))
            {
                EnumerationWarning = "WSL distribution listing timed out after 20 seconds. WSL environments were not probed."
            });

            var snapshot = await service.DetectAgentPresenceAsync(cancellationToken: CancellationToken);

            // The host environment is still probed and reported; the incomplete part stays visible.
            Assert.Contains(snapshot.Warnings, warning => warning.Contains("timed out", StringComparison.Ordinal));
            Assert.Contains(snapshot.Presence, presence => presence.EnvironmentSelector == FakeRuntime.HostEnvironment.Selector);
            var persisted = GuideAgentPresenceStore.Load(enginePaths);
            Assert.NotNull(persisted);
            Assert.Equal(snapshot.Warnings, persisted!.Warnings);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task GetStatusAsync_ReportsEnumerationWarningsAsACheck()
    {
        var root = CreateRoot();
        try
        {
            using var service = CreateService(root, new FakeRuntime(Path.Combine(root, "user-home"))
            {
                EnumerationWarning = "WSL distribution listing timed out after 20 seconds."
            });

            var report = await service.GetStatusAsync(cancellationToken: CancellationToken);

            Assert.Contains(report.Checks, check =>
                check.Id == "environment.enumeration" && check.Status == GuideCheckStatus.Warning);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"guide-presence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static AgentGuideService CreateService(string root, FakeRuntime? runtime = null)
        => new(
            KnownAgentProducts.Monica,
            new GuidePaths(Path.Combine(root, "engine-data")),
            new AgentProductPaths(Path.Combine(root, "product-data")),
            runtime ?? new FakeRuntime(Path.Combine(root, "user-home")),
            httpClient: null,
            applicationDirectory: root);

    private static void Cleanup(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
