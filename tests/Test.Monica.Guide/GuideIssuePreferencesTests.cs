using System.Text.Json;
using Monica.Guide;
using Xunit;

namespace Test.Monica.Guide;

public sealed class GuideIssuePreferencesTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public void Store_DefaultsToPrepareWithoutAFile()
    {
        using var root = new TempEngineRoot();
        var preferences = GuideIssuePreferencesStore.Load(root.Paths);

        Assert.Equal(GuideIssueReportingMode.Prepare, preferences.IssueReporting);
        Assert.False(File.Exists(root.Paths.IssuePreferencesFile));
    }

    [Fact]
    public void Store_RoundTripsEveryMode()
    {
        using var root = new TempEngineRoot();
        foreach (var mode in Enum.GetValues<GuideIssueReportingMode>())
        {
            GuideIssuePreferencesStore.Save(root.Paths, GuideIssuePreferencesStore.Load(root.Paths) with { IssueReporting = mode });

            var reloaded = GuideIssuePreferencesStore.Load(root.Paths);
            Assert.Equal(GuideIssuePreferences.CurrentSchemaVersion, reloaded.SchemaVersion);
            Assert.Equal(mode, reloaded.IssueReporting);
        }
    }

    [Fact]
    public async Task Store_RejectsUnsupportedSchemas()
    {
        using var root = new TempEngineRoot();
        GuideIssuePreferencesStore.Save(root.Paths, GuideIssuePreferences.Default);
        await File.WriteAllTextAsync(
            root.Paths.IssuePreferencesFile,
            """{"schemaVersion":99,"issueReporting":"prepare"}""",
            CancellationToken);

        Assert.Throws<InvalidDataException>(() => GuideIssuePreferencesStore.Load(root.Paths));
    }

    [Fact]
    public void Parser_AcceptsIssueStatusAndSet()
    {
        var status = GuideCommandParser.Parse(["issue", "status", "--json"]);
        Assert.Equal("issue", status.Name);
        Assert.Equal("status", status.IssueAction);
        Assert.Null(status.IssueMode);
        Assert.True(status.Json);

        var set = GuideCommandParser.Parse(["issue", "set", "--mode", "NEVER"]);
        Assert.Equal("set", set.IssueAction);
        Assert.Equal("never", set.IssueMode);
        Assert.False(set.Apply);
    }

    [Fact]
    public void Parser_RejectsInvalidIssueUsage()
    {
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["issue"]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["issue", "run"]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["issue", "set"]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["issue", "set", "--mode", "always"]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["issue", "status", "--mode", "ask"]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["status", "--mode", "ask"]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["issue", "set", "--mode", "ask", "--apply"]));
    }

    [Fact]
    public async Task Runner_StatusAndSetPersistTheMachineGlobalMode()
    {
        using var root = new TempEngineRoot();
        var output = new StringWriter();
        var error = new StringWriter();

        var status = await GuideCommandRunner.RunAsync(
            KnownAgentProducts.Monica,
            ["issue", "status", "--json"],
            output,
            error,
            CancellationToken,
            enginePaths: root.Paths,
            currentVersion: static () => "0.3.0");
        using var statusDocument = JsonDocument.Parse(output.ToString());
        var statusCheck = Assert.Single(
            statusDocument.RootElement.GetProperty("checks").EnumerateArray(),
            check => check.GetProperty("id").GetString() == "issue.reporting");
        Assert.Equal("prepare", statusCheck.GetProperty("details").GetProperty("mode").GetString());
        Assert.Equal(0, status);

        output = new StringWriter();
        var set = await GuideCommandRunner.RunAsync(
            KnownAgentProducts.Monica,
            ["issue", "set", "--mode", "never"],
            output,
            error,
            CancellationToken,
            enginePaths: root.Paths,
            currentVersion: static () => "0.3.0");
        Assert.Equal(0, set);
        Assert.Contains("never", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(GuideIssueReportingMode.Never, GuideIssuePreferencesStore.Load(root.Paths).IssueReporting);
    }

    [Fact]
    public void WorkspaceProjection_DefaultsOnAndRoundTrips()
    {
        using var root = new TempEngineRoot();
        var projection = GuideWorkspaceProjectionStore.Load(root.Paths);
        Assert.True(projection.SourceHints);
        Assert.True(projection.IssuePolicy);

        GuideWorkspaceProjectionStore.Save(root.Paths, projection with { SourceHints = false });
        var saved = GuideWorkspaceProjectionStore.Load(root.Paths);
        Assert.Equal(GuideWorkspaceProjection.CurrentSchemaVersion, saved.SchemaVersion);
        Assert.False(saved.SourceHints);
        Assert.True(saved.IssuePolicy);

        File.WriteAllText(
            root.Paths.WorkspaceProjectionFile,
            """{"schemaVersion":99,"sourceHints":true,"issuePolicy":true}""");
        Assert.Throws<InvalidDataException>(() => GuideWorkspaceProjectionStore.Load(root.Paths));
    }

    private sealed class TempEngineRoot : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"guide-issue-{Guid.NewGuid():N}");

        internal TempEngineRoot()
        {
            Directory.CreateDirectory(_root);
            Paths = new GuidePaths(_root);
        }

        internal GuidePaths Paths { get; }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
