using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Serialization;
using Xunit;

namespace Test.Monica.Configuration.Serialization;

public class ConfigurationDefinitionSchemaCodecTests
{
    [Fact]
    public void ToCompactClrTypeName_WhenTypeIsAssemblyQualifiedGeneric_ShouldUseCleanFullName()
    {
        var clrTypeName = typeof(Dictionary<string, List<int>>).AssemblyQualifiedName!;

        var compact = ConfigurationDefinitionSchemaCodec.ToCompactClrTypeName(clrTypeName);

        compact.Should().Be("System.Collections.Generic.Dictionary<System.String,System.Collections.Generic.List<System.Int32>>");
        compact.Should().NotContain("Version=");
        compact.Should().NotContain("PublicKeyToken");
    }

    [Fact]
    public void SerializeSchema_WhenDeepestNodeIsAtLogicalDepthLimit_ShouldSucceed()
    {
        var definition = CreateDefinition(64);

        var schemaJson = ConfigurationDefinitionSchemaCodec.SerializeSchema(definition);

        schemaJson.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SerializeSchema_WhenDeepestNodeExceedsLogicalDepthLimit_ShouldThrowInvalidOperationException()
    {
        var definition = CreateDefinition(65);

        var act = () => ConfigurationDefinitionSchemaCodec.SerializeSchema(definition);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*maximum logical depth of 64*")
            .WithMessage("*schema path 'Level1.Level2*");
    }

    [Fact]
    public void DeserializeDefinition_WhenDeepestNodeIsAtLogicalDepthLimit_ShouldSucceed()
    {
        var schemaJson = CreatePersistedSchemaJson(64);

        var definition = Deserialize(schemaJson);

        GetDeepestSingleChild(definition.Root).RelativePath.Depth.Should().Be(64);
    }

    [Fact]
    public void DeserializeDefinition_WhenDeepestNodeExceedsLogicalDepthLimit_ShouldRejectSchema()
    {
        var schemaJson = CreatePersistedSchemaJson(65);

        var act = () => Deserialize(schemaJson);

        act.Should().Throw<ConfigurationPersistedSchemaException>()
            .WithMessage("*maximum logical depth of 64*")
            .Which.SchemaPath.Should().EndWith("Level65");
    }

    [Fact]
    public void DeserializeDefinition_WhenPersistedJsonExceedsJsonDepthLimit_ShouldRejectPayload()
    {
        var nestedArrays = new string('[', 300) + "null" + new string(']', 300);
        var schemaJson = $$"""
                           {
                             "Name": "Root",
                             "NodeKind": 0,
                             "Overflow": {{nestedArrays}}
                           }
                           """;

        var act = () => Deserialize(schemaJson);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void CompactSchema_ShouldUsePersistedJsonDepthLimit()
    {
        ConfigurationPersistedJsonOptions.CompactSchema.MaxDepth.Should().Be(256);
    }

    [Fact]
    public void SerializeSchema_WhenScalarStringNodeHasEditorHint_ShouldPersistHint()
    {
        var schemaJson = ConfigurationDefinitionSchemaCodec.SerializeSchema(CreateEditorHintDefinition("Airway"));

        schemaJson.Should().Contain("\"EditorHint\":\"Airway\"");
    }

    [Fact]
    public void SerializeSchema_WhenNodeHasNoEditorHint_ShouldOmitEditorHintMember()
    {
        var schemaJson = ConfigurationDefinitionSchemaCodec.SerializeSchema(CreateEditorHintDefinition(null));

        schemaJson.Should().NotContain("EditorHint");
    }

    [Fact]
    public void ComputeSchemaHash_WhenEditorHintIsAddedOrChanged_ShouldChangeHash()
    {
        var withoutHint = ComputeEditorHintDefinitionHash(null);
        var withHint = ComputeEditorHintDefinitionHash("Airway");
        var withOtherHint = ComputeEditorHintDefinitionHash("Airport");

        withHint.Should().NotBe(withoutHint);
        withOtherHint.Should().NotBe(withHint);
    }

    [Fact]
    public void DeserializeDefinition_WhenScalarStringNodeHasEditorHint_ShouldRestoreHintAndKeepHashStable()
    {
        var definition = CreateEditorHintDefinition("Airway");
        var schemaJson = ConfigurationDefinitionSchemaCodec.SerializeSchema(definition);
        var schemaHash = ConfigurationDefinitionSchemaCodec.ComputeSchemaHash(
            definition.DefinitionKey,
            definition.SectionPath,
            definition.Root);

        var restored = ConfigurationDefinitionSchemaCodec.DeserializeDefinition(
            definition.DefinitionKey,
            definition.SectionPath,
            definition.DisplayName,
            null,
            typeof(object).FullName!,
            "Test.Monica.Configuration",
            null,
            1,
            schemaHash,
            ConfigurationReloadBehavior.Unknown,
            schemaJson,
            ConfigurationDefinitionOrigin.PublishedMetadata);

        restored.Root.Children[0].EditorHint.Should().Be("Airway");
        var restoredHash = ConfigurationDefinitionSchemaCodec.ComputeSchemaHash(
            restored.DefinitionKey,
            restored.SectionPath,
            restored.Root);
        restoredHash.Should().Be(schemaHash);
    }

    [Fact]
    public void SerializeSchema_WhenObjectNodeHasEditorHint_ShouldPersistHint()
    {
        var definition = CreateEditorHintDefinition(null);
        definition = definition with { Root = definition.Root with { EditorHint = "Airway" } };

        var schemaJson = ConfigurationDefinitionSchemaCodec.SerializeSchema(definition);

        schemaJson.Should().Contain("\"EditorHint\":\"Airway\"");
    }

    [Fact]
    public void DeserializeDefinition_WhenObjectNodeHasEditorHint_ShouldRestoreHint()
    {
        var schemaJson = $$"""
                           {
                             "Name": "Root",
                             "NodeKind": {{(int)ConfigurationNodeKind.Object}},
                             "EditorHint": "Airway",
                             "Children": [
                               {
                                 "Name": "Route",
                                 "NodeKind": {{(int)ConfigurationNodeKind.Scalar}},
                                 "ValueKind": {{(int)ConfigurationValueKind.String}},
                                 "ClrTypeName": "{{typeof(string).AssemblyQualifiedName}}"
                               }
                             ]
                           }
                           """;

        var definition = Deserialize(schemaJson);

        definition.Root.EditorHint.Should().Be("Airway");
    }

    [Fact]
    public void DeserializeDefinition_WhenNonStringScalarNodeHasEditorHint_ShouldRestoreHint()
    {
        var schemaJson = $$"""
                           {
                             "Name": "Root",
                             "NodeKind": {{(int)ConfigurationNodeKind.Object}},
                             "Children": [
                               {
                                 "Name": "Count",
                                 "NodeKind": {{(int)ConfigurationNodeKind.Scalar}},
                                 "ValueKind": {{(int)ConfigurationValueKind.Integer}},
                                 "ClrTypeName": "{{typeof(int).AssemblyQualifiedName}}",
                                 "EditorHint": "Airway"
                               }
                             ]
                           }
                           """;

        var definition = Deserialize(schemaJson);

        definition.Root.Children[0].EditorHint.Should().Be("Airway");
    }

    private static ConfigurationDefinition CreateEditorHintDefinition(string? editorHint)
    {
        return new ConfigurationDefinition
        {
            DefinitionKey = "test.hint",
            SectionPath = "Hint",
            DisplayName = "Hint",
            ClrTypeName = typeof(object).AssemblyQualifiedName!,
            FromProject = "Test.Monica.Configuration",
            SchemaHash = "sha256:test",
            Root = new ConfigurationNodeDefinition
            {
                NodeKey = string.Empty,
                Name = "Root",
                RelativePath = LogicalPath.Root,
                ConfigurationPath = "Hint",
                ClrTypeName = typeof(object).AssemblyQualifiedName!,
                NodeKind = ConfigurationNodeKind.Object,
                Children =
                [
                    new ConfigurationNodeDefinition
                    {
                        NodeKey = "Route",
                        Name = "Route",
                        RelativePath = LogicalPath.FromProperties("Route"),
                        ConfigurationPath = "Hint:Route",
                        ClrTypeName = typeof(string).AssemblyQualifiedName!,
                        NodeKind = ConfigurationNodeKind.Scalar,
                        ValueKind = ConfigurationValueKind.String,
                        EditorHint = editorHint
                    }
                ]
            }
        };
    }

    private static string ComputeEditorHintDefinitionHash(string? editorHint)
    {
        var definition = CreateEditorHintDefinition(editorHint);
        return ConfigurationDefinitionSchemaCodec.ComputeSchemaHash(
            definition.DefinitionKey,
            definition.SectionPath,
            definition.Root);
    }

    private static ConfigurationDefinition CreateDefinition(int deepestLogicalDepth)
    {
        return new ConfigurationDefinition
        {
            DefinitionKey = "test.depth",
            SectionPath = "Depth",
            DisplayName = "Depth",
            ClrTypeName = typeof(object).AssemblyQualifiedName!,
            FromProject = "Test.Monica.Configuration",
            SchemaHash = "sha256:test",
            Root = CreateNode(0, deepestLogicalDepth, LogicalPath.Root)
        };
    }

    private static ConfigurationNodeDefinition CreateNode(
        int depth,
        int deepestLogicalDepth,
        LogicalPath path)
    {
        var isLeaf = depth == deepestLogicalDepth;
        return new ConfigurationNodeDefinition
        {
            NodeKey = path.ToCanonicalString(),
            Name = depth == 0 ? "Root" : $"Level{depth}",
            RelativePath = path,
            ConfigurationPath = path.ToCanonicalString(),
            ClrTypeName = isLeaf ? typeof(string).AssemblyQualifiedName! : typeof(object).AssemblyQualifiedName!,
            NodeKind = isLeaf ? ConfigurationNodeKind.Scalar : ConfigurationNodeKind.Object,
            ValueKind = isLeaf ? ConfigurationValueKind.String : null,
            Children = isLeaf
                ? []
                : [CreateNode(
                    depth + 1,
                    deepestLogicalDepth,
                    path.Append(new PropertySegment($"Level{depth + 1}")))]
        };
    }

    private static string CreatePersistedSchemaJson(int deepestLogicalDepth)
    {
        JsonNode node = new JsonObject
        {
            ["Name"] = $"Level{deepestLogicalDepth}",
            ["NodeKind"] = (int)ConfigurationNodeKind.Scalar,
            ["ValueKind"] = (int)ConfigurationValueKind.String,
            ["ClrTypeName"] = typeof(string).AssemblyQualifiedName
        };

        for (var depth = deepestLogicalDepth - 1; depth >= 0; depth--)
        {
            node = new JsonObject
            {
                ["Name"] = depth == 0 ? "Root" : $"Level{depth}",
                ["NodeKind"] = (int)ConfigurationNodeKind.Object,
                ["Children"] = new JsonArray(node)
            };
        }

        return node.ToJsonString(ConfigurationPersistedJsonOptions.CompactSchema);
    }

    private static ConfigurationDefinition Deserialize(string schemaJson)
    {
        return ConfigurationDefinitionSchemaCodec.DeserializeDefinition(
            "test.depth",
            "Depth",
            "Depth",
            null,
            typeof(object).FullName!,
            "Test.Monica.Configuration",
            null,
            1,
            "sha256:test",
            ConfigurationReloadBehavior.Unknown,
            schemaJson,
            ConfigurationDefinitionOrigin.PublishedMetadata);
    }

    private static ConfigurationNodeDefinition GetDeepestSingleChild(ConfigurationNodeDefinition node)
    {
        while (node.Children.Count == 1)
        {
            node = node.Children[0];
        }

        return node;
    }
}
