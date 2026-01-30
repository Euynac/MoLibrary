namespace Monica.Framework.UI.Swagger;

/// <summary>
/// Represents a custom navigation button in Swagger UI topbar
/// </summary>
public record SwaggerNavigationButton
{
    /// <summary>
    /// Display text of the button
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Navigation path (e.g., "home" navigates to "~/home")
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Tooltip description shown on hover
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Display order (lower numbers first, default: 0)
    /// </summary>
    public int Order { get; init; } = 0;

    /// <summary>
    /// Whether enabled (default: true)
    /// </summary>
    public bool Enabled { get; init; } = true;
}
