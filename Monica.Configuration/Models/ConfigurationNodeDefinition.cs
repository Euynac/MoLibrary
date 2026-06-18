using System.Globalization;

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
    /// Gets the human-readable node name shown in management tools.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the developer-facing description shown in management tools and generated documentation.
    /// </summary>
    public string? Description { get; init; }

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

    /// <summary>
    /// Gets portable enum members when this scalar node represents an enum.
    /// </summary>
    public IReadOnlyList<ConfigurationEnumValue> EnumValues { get; init; } = [];

    /// <summary>
    /// Attempts to normalize an enum display value from either a name or numeric literal to the canonical enum name.
    /// </summary>
    /// <param name="value">The operator-provided enum value.</param>
    /// <param name="normalized">The canonical enum member name when normalization succeeds.</param>
    /// <returns>True when the value matches a known enum member.</returns>
    public bool TryNormalizeEnumDisplayValue(string? value, out string normalized)
    {
        normalized = value ?? string.Empty;
        if (ValueKind != ConfigurationValueKind.Enum || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (TryNormalizeFromPortableEnumValues(value, out normalized)
            || TryNormalizeFromRuntimeEnumType(value, out normalized))
        {
            return true;
        }

        normalized = value;
        return false;
    }

    private bool TryNormalizeFromPortableEnumValues(string value, out string normalized)
    {
        foreach (var enumValue in EnumValues)
        {
            if (string.Equals(enumValue.Name, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(enumValue.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                normalized = enumValue.Name;
                return true;
            }
        }

        normalized = value;
        return false;
    }

    private bool TryNormalizeFromRuntimeEnumType(string value, out string normalized)
    {
        var enumType = Type.GetType(ClrTypeName, throwOnError: false);
        if (enumType?.IsEnum is not true)
        {
            normalized = value;
            return false;
        }

        foreach (var name in Enum.GetNames(enumType))
        {
            if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ToInvariantEnumNumber(Enum.Parse(enumType, name)), value, StringComparison.OrdinalIgnoreCase))
            {
                normalized = name;
                return true;
            }
        }

        normalized = value;
        return false;
    }

    private static string ToInvariantEnumNumber(object enumValue)
    {
        var underlyingType = Enum.GetUnderlyingType(enumValue.GetType());
        var numericValue = Convert.ChangeType(enumValue, underlyingType, CultureInfo.InvariantCulture);
        return Convert.ToString(numericValue, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
