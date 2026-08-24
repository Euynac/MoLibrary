using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.AI.Mcp.Abstractions;
using Monica.AI.Mcp.Models;
using Monica.AI.Mcp.Services;
using Monica.Core.Skills.Annotations;
using Monica.Core.XmlDocumentation.Abstractions;
using Monica.Core.XmlDocumentation.Models;
using Monica.Modules;
using Monica.AI.Services.Support.ModuleCatalog;

namespace Test.Monica.AI.Services;

public sealed class McpSkillPackExporterTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public void DeriveNames_ShouldUseStableServerIdentity()
    {
        McpSkillPackExporter.DeriveSkillName("monica-workflow").Should().Be("monica-workflow-operations");
        McpSkillPackExporter.DeriveEndpointEnvironmentVariable("monica-workflow")
            .Should().Be("MONICA_WORKFLOW_MCP_URL");
    }

    [Fact]
    public void Render_ShouldProduceDeterministicPackWithAllSurfaces()
    {
        var exporter = CreateExporter(new TestWorkflowServer());
        var endpoint = new Uri("http://localhost:61345/mcp/monica-workflow");

        var first = exporter.Render("monica-workflow", endpoint);
        var second = exporter.Render("monica-workflow", endpoint);

        first.SkillName.Should().Be("monica-workflow-operations");
        first.ServerVersion.Should().Be("9.9.9");
        first.EndpointUrl.Should().Be("http://localhost:61345/mcp/monica-workflow");
        first.EndpointEnvironmentVariable.Should().Be("MONICA_WORKFLOW_MCP_URL");
        first.ToolCount.Should().Be(2);
        first.ToolSchemaDigest.Should().StartWith("sha256:");
        second.Files.Should().BeEquivalentTo(first.Files);
        first.Files.Select(static file => file.RelativePath)
            .Should().BeEquivalentTo(["SKILL.md", "references/tools.md", "scripts/mcp-call.sh", "scripts/mcp-call.ps1"]);

        var skill = Content(first, "SKILL.md");
        skill.Should().Contain("name: monica-workflow-operations");
        skill.Should().Contain("http://localhost:61345/mcp/monica-workflow");
        skill.Should().Contain("MONICA_WORKFLOW_MCP_URL");
        skill.Should().Contain("`list-things`");
        skill.Should().NotContain("\r");

        Content(first, "references/tools.md").Should().Contain("`echo-message`").And.NotContain("\r");
        Content(first, "scripts/mcp-call.sh").Should().Contain("MONICA_WORKFLOW_MCP_URL").And.NotContain("\r");
        Content(first, "scripts/mcp-call.ps1").Should().Contain("$env:MONICA_WORKFLOW_MCP_URL").And.NotContain("\r");
    }

    [Fact]
    public void Render_ShouldRejectUnknownAndNonHttpServers()
    {
        var exporter = CreateExporter(new TestWorkflowServer(), new TestStdioServer());

        var act = () => exporter.Render("missing", new Uri("http://localhost:1/mcp/missing"));
        act.Should().Throw<KeyNotFoundException>();

        var stdio = () => exporter.Render("test-stdio", new Uri("http://localhost:1/mcp/test-stdio"));
        stdio.Should().Throw<InvalidOperationException>().WithMessage("*HTTP transport*");

        var badEndpoint = () => exporter.Render("monica-workflow", new Uri("ftp://localhost/mcp"));
        badEndpoint.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WriteToDirectory_ShouldWritePackOnceAndRefuseExistingDirectory()
    {
        var exporter = CreateExporter(new TestWorkflowServer());
        var pack = exporter.Render("monica-workflow", new Uri("http://localhost:61345/mcp/monica-workflow"));

        var result = exporter.WriteToDirectory(pack, _temp.Root);

        result.SkillDirectoryPath.Should().Be(Path.Combine(_temp.Root, "monica-workflow-operations"));
        result.WrittenFiles.Should().HaveCount(4);
        foreach (var file in pack.Files)
        {
            var path = Path.Combine(result.SkillDirectoryPath, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(path).Should().BeTrue();
            File.ReadAllText(path).Should().Be(file.Content);
        }

        var second = () => exporter.WriteToDirectory(pack, _temp.Root);
        second.Should().Throw<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public void ComputeDigest_ShouldNormalizeToolOrderAndPropertyOrder()
    {
        var unordered = ParseTools("""
            [
              {"name":"zeta","description":"last","inputSchema":{"type":"object","properties":{"b":{"type":"string"},"a":{"type":"number"}}}},
              {"name":"alpha","description":"first","inputSchema":{"type":"object"}}
            ]
            """);
        var reordered = ParseTools("""
            [
              {"inputSchema":{"type":"object"},"description":"first","name":"alpha"},
              {"name":"zeta","inputSchema":{"type":"object","properties":{"b":{"type":"string"},"a":{"type":"number"}}},"description":"last"}
            ]
            """);

        McpToolSchemaDigest.Compute(unordered).Should().Be(McpToolSchemaDigest.Compute(reordered));

        var changed = ParseTools("""
            [
              {"name":"alpha","description":"changed","inputSchema":{"type":"object"}},
              {"name":"zeta","description":"last","inputSchema":{"type":"object","properties":{"b":{"type":"string"},"a":{"type":"number"}}}}
            ]
            """);
        McpToolSchemaDigest.Compute(changed).Should().NotBe(McpToolSchemaDigest.Compute(unordered));

        var duplicate = ParseTools("""
            [
              {"name":"alpha","description":"first"},
              {"name":"alpha","description":"first"}
            ]
            """);
        var act = () => McpToolSchemaDigest.Compute(duplicate);
        act.Should().Throw<InvalidDataException>();
    }

    private static string Content(McpSkillPack pack, string relativePath)
    {
        return pack.Files.Single(file => file.RelativePath == relativePath).Content;
    }

    private static List<System.Text.Json.JsonElement> ParseTools(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray().Select(static element => element.Clone()).ToList();
    }

    private static McpSkillPackExporter CreateExporter(params McpServer[] servers)
    {
        var options = Options.Create(new ModuleMcpOption());
        var loggerFactory = NullLoggerFactory.Instance;
        var catalog = new MonicaMcpCatalog(
            servers,
            [],
            [],
            new FileExternalMcpClientProfileStore(options, NullLogger<FileExternalMcpClientProfileStore>.Instance),
            new ExternalMcpClientFactory(loggerFactory),
            new EmptyLoadedModuleCatalog(),
            new NoXmlDocumentation(),
            options,
            new ServiceCollection().BuildServiceProvider(),
            loggerFactory);
        return new McpSkillPackExporter(catalog);
    }

    private sealed class TestWorkflowServer : McpServer<TestWorkflowServer>
    {
        public override McpServerDefinition Definition { get; } =
            new("monica-workflow", "Test workflow server for pack export") { Version = "9.9.9" };

        [SkillTool(Name = "list-things", Description = "Lists governed things.")]
        public IReadOnlyList<string> ListThings(string projectPath)
        {
            return [projectPath];
        }

        [SkillTool(Name = "echo-message", Description = "Echoes one message back.")]
        public string EchoMessage(string message)
        {
            return message;
        }
    }

    private sealed class TestStdioServer : McpServer<TestStdioServer>
    {
        public override McpServerDefinition Definition { get; } = new("test-stdio", "Stdio test server");

        public override McpServerTransportKind TransportKind => McpServerTransportKind.Stdio;

        [SkillTool(Name = "stdio-tool", Description = "Stdio only.")]
        public string StdioTool()
        {
            return "ok";
        }
    }

    private sealed class EmptyLoadedModuleCatalog : ILoadedModuleCatalog
    {
        public IReadOnlySet<Type> GetLoadedModuleTypes()
        {
            return FrozenEmptySet();
        }

        private static IReadOnlySet<Type> FrozenEmptySet()
        {
            return System.Collections.Frozen.FrozenSet<Type>.Empty;
        }
    }

    private sealed class NoXmlDocumentation : IXmlDocumentationService
    {
        public XmlMethodDocumentation? GetMethodDocumentation(MethodInfo method)
        {
            return null;
        }

        public string? GetTypeDocumentation(Type type)
        {
            return null;
        }

        public void ClearCache()
        {
        }

        public IReadOnlyList<XmlDocumentCacheInfo> GetCachedDocuments()
        {
            return [];
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "monica-skill-pack-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // Temp cleanup is best effort.
            }
            catch (UnauthorizedAccessException)
            {
                // Temp cleanup is best effort.
            }
        }
    }
}
