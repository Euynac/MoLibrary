using Monica.Configuration.Models;

namespace Monica.Configuration.Annotations;

/// <summary>
/// Adds Monica-specific metadata to a configuration property without replacing standard .NET validation attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class OptionSettingAttribute : Attribute
{
    /// <summary>
    /// Initializes a new option setting attribute.
    /// </summary>
    public OptionSettingAttribute()
    {
    }

    /// <summary>
    /// Initializes a new option setting attribute with a display name.
    /// </summary>
    /// <param name="displayName">The display name shown in management tools.</param>
    public OptionSettingAttribute(string displayName)
    {
        DisplayName = displayName;
    }

    /// <summary>
    /// Gets or sets a stable node key that survives CLR property renames.
    /// </summary>
    public string? NodeKey { get; set; }

    /// <summary>
    /// Gets or sets the display name shown in management tools.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the developer-facing description shown in management tools.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether the value is sensitive and must be redacted outside the final options binding path.
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Gets or sets an optional reload behavior override for this node.
    /// </summary>
    public ConfigurationReloadBehavior ReloadBehavior { get; set; } = ConfigurationReloadBehavior.Inherit;

    /// <summary>
    /// Gets or sets whether this scalar property is the stable identity for items inside a list.
    /// Exactly one property on a list item type may set this value to <see langword="true"/>.
    /// </summary>
    public bool IsListItemKey { get; set; }
}
