using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Resolves logical paths through configuration schemas and preserves ancestor-level sensitivity semantics.
/// </summary>
internal static class ConfigurationSchemaNavigator
{
    public static ConfigurationNodeDefinition? ResolveNode(
        ConfigurationNodeDefinition root,
        LogicalPath logicalPath)
    {
        var current = root;
        foreach (var segment in logicalPath.Segments)
        {
            current = ResolveChild(current, segment);
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    public static bool IsSensitivePath(
        ConfigurationNodeDefinition root,
        LogicalPath logicalPath,
        bool treatUnknownAsSensitive = true)
    {
        var current = root;
        if (current.IsSensitive)
        {
            return true;
        }

        foreach (var segment in logicalPath.Segments)
        {
            current = ResolveChild(current, segment);
            if (current is null)
            {
                return treatUnknownAsSensitive;
            }

            if (current.IsSensitive)
            {
                return true;
            }
        }

        return false;
    }

    private static ConfigurationNodeDefinition? ResolveChild(
        ConfigurationNodeDefinition parent,
        ConfigurationPathSegment segment)
    {
        return segment switch
        {
            PropertySegment property => parent.Children.FirstOrDefault(child =>
                string.Equals(child.Name, property.Name, StringComparison.OrdinalIgnoreCase)),
            DictionaryKeySegment => parent.DictionaryTemplate?.ValueTemplate,
            ListItemKeySegment or ListIndexSegment => parent.ListTemplate?.ItemTemplate,
            _ => null
        };
    }
}
