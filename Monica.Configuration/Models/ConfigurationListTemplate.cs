namespace Monica.Configuration.Models;

/// <summary>
/// Defines list item-template metadata.
/// </summary>
public sealed record ConfigurationListTemplate
{
    /// <summary>
    /// Gets the schema template for list items.
    /// </summary>
    public required ConfigurationNodeDefinition ItemTemplate { get; init; }

    /// <summary>
    /// Gets the item property used as stable identity for per-item mutation.
    /// </summary>
    public string? ItemKeyPropertyName { get; init; }

    /// <summary>
    /// Gets whether the list supports per-item mutation.
    /// </summary>
    public bool SupportsPerItemMutation => !string.IsNullOrWhiteSpace(ItemKeyPropertyName);
}
