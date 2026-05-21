using Monica.Configuration.Models;

namespace Monica.Configuration.Providers;

/// <summary>
/// Enumerates schema nodes that can be read from hierarchical Microsoft configuration data.
/// </summary>
public static class ConfigurationSourceNodeEnumerator
{
    /// <summary>
    /// Enumerates scalar leaves with their logical and configuration paths.
    /// </summary>
    /// <param name="definition">The schema definition.</param>
    /// <returns>Scalar leaf descriptors.</returns>
    public static IReadOnlyList<ConfigurationSourceLeaf> EnumerateLeaves(ConfigurationDefinition definition)
    {
        var leaves = new List<ConfigurationSourceLeaf>();
        EnumerateNode(definition.Root, leaves);
        return leaves;
    }

    private static void EnumerateNode(ConfigurationNodeDefinition node, ICollection<ConfigurationSourceLeaf> leaves)
    {
        switch (node.NodeKind)
        {
            case ConfigurationNodeKind.Scalar:
                if (node.ConfigurationPath is not null)
                {
                    leaves.Add(new ConfigurationSourceLeaf(node.RelativePath, node.ConfigurationPath));
                }
                break;
            case ConfigurationNodeKind.Object:
                foreach (var child in node.Children)
                {
                    EnumerateNode(child, leaves);
                }
                break;
            case ConfigurationNodeKind.Dictionary:
            case ConfigurationNodeKind.List:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(node), node.NodeKind, "Unsupported configuration node kind.");
        }
    }

}

/// <summary>
/// Describes a schema leaf readable from a hierarchical source.
/// </summary>
/// <param name="LogicalPath">The logical leaf path.</param>
/// <param name="ConfigurationPath">The Microsoft configuration path.</param>
public sealed record ConfigurationSourceLeaf(LogicalPath LogicalPath, string ConfigurationPath);
