using Monica.Configuration.Models;

namespace Test.Monica.Configuration;

internal static class TestConfigurationFactory
{
    public const string DefinitionKey = "Test.AppOptions";

    public static ConfigurationSourceDescriptor Source(
        string sourceKey,
        int priority,
        bool isWritable = true)
    {
        return new ConfigurationSourceDescriptor
        {
            SourceKey = sourceKey,
            DisplayName = sourceKey,
            Kind = ConfigurationSourceKind.Memory,
            Priority = priority,
            IsWritable = isWritable
        };
    }

    public static ConfigurationValueOverride Override(
        LogicalPath path,
        string sourceKey = "memory:test",
        string definitionKey = DefinitionKey,
        string json = "\"value\"",
        ConfigurationValueState state = ConfigurationValueState.Active,
        ConfigurationOverrideGranularity granularity = ConfigurationOverrideGranularity.Scalar,
        long version = 1)
    {
        return new ConfigurationValueOverride
        {
            OverrideId = Guid.NewGuid().ToString("N"),
            DefinitionKey = definitionKey,
            LogicalPath = path,
            ConfigurationPath = null,
            SourceKey = sourceKey,
            Granularity = granularity,
            State = state,
            Value = ConfigurationStoredValue.Plain(json),
            Version = version,
            SchemaVersion = 1,
            LastModifiedTime = DateTimeOffset.UtcNow
        };
    }

    public static ConfigurationDefinition Definition()
    {
        return new ConfigurationDefinition
        {
            DefinitionKey = DefinitionKey,
            SectionPath = "Test:App",
            DisplayName = "Test App",
            ClrTypeName = typeof(TestConfigurationFactory).AssemblyQualifiedName!,
            SchemaHash = "sha256:test",
            Root = RootNode()
        };
    }

    public static ConfigurationNodeDefinition RootNode()
    {
        return new ConfigurationNodeDefinition
        {
            NodeKey = string.Empty,
            Name = "AppOptions",
            RelativePath = LogicalPath.Root,
            ConfigurationPath = "Test:App",
            ClrTypeName = typeof(object).AssemblyQualifiedName!,
            NodeKind = ConfigurationNodeKind.Object,
            IsNullable = false,
            Children =
            [
                ScalarNode("WorkerId", typeof(int), ConfigurationValueKind.Integer),
                ObjectNode("Services",
                [
                    ScalarNode("Name", typeof(string), ConfigurationValueKind.String),
                    ScalarNode("ConnectionString", typeof(string), ConfigurationValueKind.String),
                    ObjectNode("Nested",
                    [
                        ScalarNode("Enabled", typeof(bool), ConfigurationValueKind.Boolean)
                    ])
                ])
            ]
        };
    }

    public static ConfigurationNodeDefinition ScalarNode(
        string name,
        Type type,
        ConfigurationValueKind valueKind,
        LogicalPath? path = null,
        string? configurationPath = null)
    {
        var relativePath = path ?? LogicalPath.FromProperties(name);
        return new ConfigurationNodeDefinition
        {
            NodeKey = relativePath.ToCanonicalString(),
            Name = name,
            RelativePath = relativePath,
            ConfigurationPath = configurationPath ?? $"Test:App:{name}",
            ClrTypeName = type.AssemblyQualifiedName ?? type.FullName ?? type.Name,
            NodeKind = ConfigurationNodeKind.Scalar,
            ValueKind = valueKind,
            IsNullable = !type.IsValueType || Nullable.GetUnderlyingType(type) is not null
        };
    }

    public static ConfigurationNodeDefinition ObjectNode(
        string name,
        IReadOnlyList<ConfigurationNodeDefinition> children,
        LogicalPath? path = null,
        string? configurationPath = null)
    {
        var relativePath = path ?? LogicalPath.FromProperties(name);
        return new ConfigurationNodeDefinition
        {
            NodeKey = relativePath.ToCanonicalString(),
            Name = name,
            RelativePath = relativePath,
            ConfigurationPath = configurationPath ?? $"Test:App:{name}",
            ClrTypeName = typeof(object).AssemblyQualifiedName!,
            NodeKind = ConfigurationNodeKind.Object,
            IsNullable = true,
            Children = children
        };
    }
}
