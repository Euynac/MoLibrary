using AwesomeAssertions;
using Monica.Modules;

namespace Test.Monica.AI.Modules;

public sealed class ModuleMcpTests
{
    [Theory]
    [InlineData("/mcp", "workflow", "/mcp/workflow")]
    [InlineData("tools/mcp/", "server with spaces", "/tools/mcp/server%20with%20spaces")]
    [InlineData("/", "workflow", "/workflow")]
    public void CreateHttpEndpointPath_ShouldUseTheFrameworkEndpointProjection(
        string basePath,
        string serverName,
        string expected)
    {
        var options = new ModuleMcpOption
        {
            McpHttpEndpointPath = basePath
        };

        options.CreateHttpEndpointPath(serverName).Should().Be(expected);
    }
}
