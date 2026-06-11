using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationNodeShapeExtensions
{
    public static bool IsScalarCollection(this ConfigurationNodeDefinition node)
    {
        return node.IsScalarList() || node.IsScalarDictionary();
    }

    public static bool IsScalarList(this ConfigurationNodeDefinition node)
    {
        return node.NodeKind == ConfigurationNodeKind.List
               && node.ListTemplate?.ItemTemplate.NodeKind == ConfigurationNodeKind.Scalar;
    }

    public static bool IsScalarDictionary(this ConfigurationNodeDefinition node)
    {
        return node.NodeKind == ConfigurationNodeKind.Dictionary
               && node.DictionaryTemplate is { KeyKind: ConfigurationValueKind.String, ValueTemplate.NodeKind: ConfigurationNodeKind.Scalar };
    }
}
