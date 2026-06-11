using Monica.Configuration.Models;

namespace Test.Monica.Configuration;

internal static class TestConfigurationFactory
{
    public const string DefinitionKey = "Test.AppOptions";
    public const string FromProject = "Test.Project";

    public static ConfigurationDefinition Definition()
    {
        return new ConfigurationDefinition
        {
            DefinitionKey = DefinitionKey,
            SectionPath = "Test:App",
            DisplayName = "Test App",
            ClrTypeName = typeof(TestConfigurationFactory).AssemblyQualifiedName!,
            FromProject = FromProject,
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
                ListNode(
                    "Services",
                    ServiceItemNode(new LogicalPath([new PropertySegment("Services"), new ListItemKeySegment("*")]), "Test:App:Services:*"),
                    "Name"),
                DictionaryNode(
                    "ServiceMap",
                    ObjectNode(
                        "Value",
                        [
                            ListNode(
                                "ConnectedDbs",
                                ConnectedDbItemNode(
                                    new LogicalPath(
                                    [
                                        new PropertySegment("ServiceMap"),
                                        new DictionaryKeySegment("*"),
                                        new PropertySegment("ConnectedDbs"),
                                        new ListItemKeySegment("*")
                                    ]),
                                    "Test:App:ServiceMap:*:ConnectedDbs:*"),
                                "Name")
                        ],
                        new LogicalPath([new PropertySegment("ServiceMap"), new DictionaryKeySegment("*")]),
                        "Test:App:ServiceMap:*"),
                    LogicalPath.FromProperties("ServiceMap"),
                    "Test:App:ServiceMap"),
                ListNode(
                    "UnkeyedServices",
                    ServiceItemNode(new LogicalPath([new PropertySegment("UnkeyedServices"), new ListItemKeySegment("*")]), "Test:App:UnkeyedServices:*"),
                    null)
            ]
        };
    }

    public static LogicalPath ServiceItemPath(string itemKey)
    {
        return new LogicalPath([new PropertySegment("Services"), new ListItemKeySegment(itemKey)]);
    }

    public static LogicalPath ServiceMapPath(string serviceKey)
    {
        return new LogicalPath([new PropertySegment("ServiceMap"), new DictionaryKeySegment(serviceKey)]);
    }

    public static LogicalPath ConnectedDbPath(string serviceKey, string itemKey)
    {
        return new LogicalPath(
        [
            new PropertySegment("ServiceMap"),
            new DictionaryKeySegment(serviceKey),
            new PropertySegment("ConnectedDbs"),
            new ListItemKeySegment(itemKey)
        ]);
    }

    public static ConfigurationNodeDefinition ScalarNode(
        string name,
        Type type,
        ConfigurationValueKind valueKind,
        LogicalPath? path = null,
        string? configurationPath = null,
        bool isSensitive = false)
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
            IsNullable = !type.IsValueType || Nullable.GetUnderlyingType(type) is not null,
            IsSensitive = isSensitive
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

    public static ConfigurationNodeDefinition ListNode(
        string name,
        ConfigurationNodeDefinition itemTemplate,
        string? itemKeyPropertyName,
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
            ClrTypeName = typeof(List<object>).AssemblyQualifiedName!,
            NodeKind = ConfigurationNodeKind.List,
            IsNullable = true,
            ListTemplate = new ConfigurationListTemplate
            {
                ItemKeyPropertyName = itemKeyPropertyName,
                ItemTemplate = itemTemplate
            }
        };
    }

    public static ConfigurationNodeDefinition DictionaryNode(
        string name,
        ConfigurationNodeDefinition valueTemplate,
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
            ClrTypeName = typeof(Dictionary<string, object>).AssemblyQualifiedName!,
            NodeKind = ConfigurationNodeKind.Dictionary,
            IsNullable = true,
            DictionaryTemplate = new ConfigurationDictionaryTemplate
            {
                KeyClrTypeName = typeof(string).AssemblyQualifiedName!,
                KeyKind = ConfigurationValueKind.String,
                ValueTemplate = valueTemplate
            }
        };
    }

    private static ConfigurationNodeDefinition ServiceItemNode(LogicalPath path, string configurationPath)
    {
        return ObjectNode(
            "Item",
            [
                ScalarNode("Name", typeof(string), ConfigurationValueKind.String),
                ScalarNode("ConnectionString", typeof(string), ConfigurationValueKind.String),
                ObjectNode("Nested",
                [
                    ScalarNode("Enabled", typeof(bool), ConfigurationValueKind.Boolean)
                ])
            ],
            path,
            configurationPath);
    }

    private static ConfigurationNodeDefinition ConnectedDbItemNode(LogicalPath path, string configurationPath)
    {
        return ObjectNode(
            "Item",
            [
                ScalarNode("Name", typeof(string), ConfigurationValueKind.String),
                ScalarNode("ConnectionString", typeof(string), ConfigurationValueKind.String)
            ],
            path,
            configurationPath);
    }
}
