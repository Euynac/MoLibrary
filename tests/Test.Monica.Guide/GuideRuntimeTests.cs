using Monica.Guide;
using Xunit;

namespace Test.Monica.Guide;

public sealed class WorkflowGuideRuntimeTests
{
    [Fact]
    public async Task ProbeRuntimeAsync_ReportsLoopbackFailuresWithoutConfigurationInspection()
    {
        var root = Path.Combine(Path.GetTempPath(), $"workflow-runtime-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // An always-refusing handler answers instantly and deterministically; probing a
            // real unbound port depends on host firewall behavior and can take seconds.
            using var http = new HttpClient(new RefusingHandler());
            using var service = new AgentGuideService(
                KnownAgentProducts.MonicaWorkflow,
                enginePaths: new GuidePaths(Path.Combine(root, "engine-data")),
                productPaths: new AgentProductPaths(Path.Combine(root, "product-data")),
                httpClient: http,
                applicationDirectory: root);

            var report = await service.ProbeRuntimeAsync(
                baseAddress: new Uri("http://127.0.0.1:1"),
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(GuideStatus.Error, report.Status);
            var ids = report.Checks.Select(static check => check.Id).ToArray();
            Assert.Contains("runtime.healthz", ids);
            Assert.Contains("runtime.ui", ids);
            Assert.Contains("runtime.mcp", ids);
            Assert.DoesNotContain(ids, id => id.StartsWith("configuration.", StringComparison.Ordinal));
            Assert.DoesNotContain(ids, id => id.StartsWith("agent.", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class RefusingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("No connection could be made (test stub).");
    }

    [Fact]
    public void BuildWslWriteScript_QuotesDestinationDirectoryContainingSpaces()
    {
        const string path = "/home/test/custom agent/skills/monica-workflow-guide/SKILL.md";
        const string temporary = "/home/test/custom agent/skills/monica-workflow-guide/SKILL.md.tmp";

        var script = GuideEnvironmentRuntime.BuildWslWriteScript(
            path,
            temporary,
            "cGF5bG9hZA==");

        Assert.StartsWith(
            "mkdir -p -- \"$(dirname -- '/home/test/custom agent/skills/monica-workflow-guide/SKILL.md')\" && ",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain("mkdir -p -- $(dirname", script, StringComparison.Ordinal);
    }
}
