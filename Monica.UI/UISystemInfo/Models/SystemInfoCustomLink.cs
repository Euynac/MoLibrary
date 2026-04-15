using Microsoft.Extensions.Localization;
using Monica.Core.Localization.Models;

namespace Monica.UI.UISystemInfo.Models;

/// <summary>
/// Represents a custom shortcut link shown on the system information page.
/// </summary>
public record SystemInfoCustomLink
{
    /// <summary>
     /// Gets the display name of the link.
    /// </summary>
    public required LocalizedText Name { get; init; }

    /// <summary>
    /// Gets the link URL. Supports relative and absolute URLs.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the MudBlazor Material icon name.
    /// </summary>
    public string Icon { get; init; } = MudBlazor.Icons.Material.Filled.Link;

    /// <summary>
    /// Gets the optional description shown under the link.
    /// </summary>
    public LocalizedText? Description { get; init; }

    /// <summary>
    /// Gets the optional category used to group links together.
    /// </summary>
    public LocalizedText? Category { get; init; }

    /// <summary>
    /// Gets the display order. Smaller values appear first.
    /// </summary>
    public int Order { get; init; } = 0;

    /// <summary>
    /// Gets a value indicating whether the link is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the target behavior used to render the anchor element.
    /// </summary>
    public SystemInfoCustomLinkTarget Target { get; init; } = SystemInfoCustomLinkTarget.NewTab;

    /// <summary>
    /// Gets the optional user name shown for quick copy.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// Gets the optional password exposed only through a copy action.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Gets a value indicating whether the link has a visible user name.
    /// </summary>
    public bool HasUserName => !string.IsNullOrWhiteSpace(UserName);

    /// <summary>
    /// Gets a value indicating whether the link has a password copy action.
    /// </summary>
    public bool HasPassword => !string.IsNullOrWhiteSpace(Password);

    /// <summary>
    /// Gets a value indicating whether any credential-related UI should be shown.
    /// </summary>
    public bool HasCredentials => HasUserName || HasPassword;

    /// <summary>
    /// Resolves the display name using the provided localizer.
    /// </summary>
    /// <param name="localizer">The localizer used to resolve the configured text.</param>
    /// <returns>The localized display name.</returns>
    public string GetDisplayName(IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return Name.Resolve(localizer);
    }

    /// <summary>
    /// Resolves the optional description using the provided localizer.
    /// </summary>
    /// <param name="localizer">The localizer used to resolve the configured text.</param>
    /// <returns>The localized description, or null when no description exists.</returns>
    public string? GetDescription(IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return Description?.Resolve(localizer);
    }

    /// <summary>
    /// Resolves the optional category using the provided localizer.
    /// </summary>
    /// <param name="localizer">The localizer used to resolve the configured text.</param>
    /// <returns>The localized category, trimmed for grouping, or null when no category exists.</returns>
    public string? GetCategory(IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        var category = Category?.Resolve(localizer);
        return string.IsNullOrWhiteSpace(category)
            ? null
            : category.Trim();
    }
}
