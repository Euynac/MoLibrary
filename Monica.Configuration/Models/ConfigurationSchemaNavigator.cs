namespace Monica.Configuration.Models;

/// <summary>
/// Resolves logical paths through configuration schemas and preserves ancestor-level metadata semantics.
/// </summary>
internal static class ConfigurationSchemaNavigator
{
    public static ConfigurationNodeDefinition? ResolveNode(
        ConfigurationNodeDefinition root,
        LogicalPath logicalPath)
    {
        return ResolvePath(root, logicalPath)?.Node;
    }

    /// <summary>
    /// Resolves a logical path to its schema node, schema-canonical path, and nearest explicit reload-behavior override.
    /// </summary>
    public static ConfigurationSchemaPathResolution? ResolvePath(
        ConfigurationNodeDefinition root,
        LogicalPath logicalPath)
    {
        var current = root;
        var canonicalSegments = new List<ConfigurationPathSegment>(logicalPath.Depth);
        var reloadBehavior = ExplicitReloadBehavior(current.ReloadBehavior);
        foreach (var segment in logicalPath.Segments)
        {
            current = ResolveChild(current, segment);
            if (current is null)
            {
                return null;
            }

            canonicalSegments.Add(CanonicalizeSegment(segment, current));
            reloadBehavior = ExplicitReloadBehavior(current.ReloadBehavior) ?? reloadBehavior;
        }

        return new ConfigurationSchemaPathResolution(
            current,
            new LogicalPath(canonicalSegments),
            reloadBehavior);
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

    private static ConfigurationPathSegment CanonicalizeSegment(
        ConfigurationPathSegment requestedSegment,
        ConfigurationNodeDefinition resolvedNode)
    {
        return requestedSegment is PropertySegment
            ? new PropertySegment(resolvedNode.Name)
            : requestedSegment;
    }

    private static ConfigurationReloadBehavior? ExplicitReloadBehavior(
        ConfigurationReloadBehavior? reloadBehavior)
    {
        return reloadBehavior is null or ConfigurationReloadBehavior.Inherit
            ? null
            : reloadBehavior;
    }
}

/// <summary>
/// Describes one resolved schema path and the metadata inherited along that path.
/// </summary>
internal sealed record ConfigurationSchemaPathResolution(
    ConfigurationNodeDefinition Node,
    LogicalPath CanonicalPath,
    ConfigurationReloadBehavior? ReloadBehaviorOverride);
