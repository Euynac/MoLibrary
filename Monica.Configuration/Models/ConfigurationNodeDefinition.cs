namespace Monica.Configuration.Models;

/// <summary>
/// Defines one node in a configuration schema tree.
/// </summary>
public sealed record ConfigurationNodeDefinition
{
    /// <summary>
    /// Gets the stable node key within the owning definition.
    /// </summary>
    public required string NodeKey { get; init; }

    /// <summary>
    /// Gets the CLR property or logical node name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the node path relative to the definition root.
    /// </summary>
    public required LogicalPath RelativePath { get; init; }

    /// <summary>
    /// Gets the full Microsoft configuration path, when it can be known from schema alone.
    /// </summary>
    public string? ConfigurationPath { get; init; }

    /// <summary>
    /// Gets the CLR type name represented by this node.
    /// </summary>
    public required string ClrTypeName { get; init; }

    /// <summary>
    /// Gets the structural node kind.
    /// </summary>
    public ConfigurationNodeKind NodeKind { get; init; }

    /// <summary>
    /// Gets the scalar value kind when <see cref="NodeKind"/> is scalar.
    /// </summary>
    public ConfigurationValueKind? ValueKind { get; init; }

    /// <summary>
    /// Gets whether null is a valid value.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Gets whether the value should be redacted outside final binding.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Gets a reload behavior override for this node.
    /// </summary>
    public ConfigurationReloadBehavior? ReloadBehavior { get; init; }

    /// <summary>
    /// Gets dictionary metadata when this node is a dictionary.
    /// </summary>
    public ConfigurationDictionaryTemplate? DictionaryTemplate { get; init; }

    /// <summary>
    /// Gets list metadata when this node is a list.
    /// </summary>
    public ConfigurationListTemplate? ListTemplate { get; init; }

    /// <summary>
    /// Gets object property child nodes.
    /// </summary>
    public IReadOnlyList<ConfigurationNodeDefinition> Children { get; init; } = [];

    /// <summary>
    /// Gets validation rules discovered from standard .NET validation metadata.
    /// </summary>
    public IReadOnlyList<ConfigurationValidationRule> ValidationRules { get; init; } = [];
}
