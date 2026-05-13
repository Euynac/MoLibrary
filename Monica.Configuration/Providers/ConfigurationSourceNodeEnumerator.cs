using Microsoft.Extensions.Configuration;
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

    /// <summary>
    /// Reads a scalar value from a configuration root and returns a source override when a value exists.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="leaf">The scalar leaf.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <param name="sourceKey">The source key.</param>
    /// <returns>A source override, or null when the source has no value at the path.</returns>
    public static ConfigurationValueOverride? ReadLeaf(
        ConfigurationDefinition definition,
        ConfigurationSourceLeaf leaf,
        IConfiguration configuration,
        string sourceKey)
    {
        var value = configuration[leaf.ConfigurationPath];
        if (value is null)
        {
            return null;
        }

        return CreateOverride(definition, leaf, value, sourceKey);
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

    private static ConfigurationValueOverride CreateOverride(
        ConfigurationDefinition definition,
        ConfigurationSourceLeaf leaf,
        string value,
        string sourceKey)
    {
        return new ConfigurationValueOverride
        {
            OverrideId = $"{sourceKey}:{definition.DefinitionKey}:{leaf.LogicalPath.ToCanonicalString()}",
            DefinitionKey = definition.DefinitionKey,
            LogicalPath = leaf.LogicalPath,
            ConfigurationPath = leaf.ConfigurationPath,
            SourceKey = sourceKey,
            Granularity = ConfigurationOverrideGranularity.Scalar,
            State = ConfigurationValueState.Active,
            Value = ConfigurationStoredValue.Plain(System.Text.Json.JsonSerializer.Serialize(value)),
            Version = 0,
            SchemaVersion = definition.SchemaVersion,
            LastModifiedTime = DateTimeOffset.MinValue
        };
    }
}

/// <summary>
/// Describes a schema leaf readable from a hierarchical source.
/// </summary>
/// <param name="LogicalPath">The logical leaf path.</param>
/// <param name="ConfigurationPath">The Microsoft configuration path.</param>
public sealed record ConfigurationSourceLeaf(LogicalPath LogicalPath, string ConfigurationPath);
