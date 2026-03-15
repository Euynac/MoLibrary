namespace Monica.Framework.UI.UISystemInfo.Models;

/// <summary>
/// Represents a grouped set of custom system information links.
/// </summary>
/// <param name="Title">Optional group title.</param>
/// <param name="Links">Links that belong to the group.</param>
public sealed record SystemInfoCustomLinkGroup(string? Title, IReadOnlyList<SystemInfoCustomLink> Links)
{
    /// <summary>
    /// Gets a value indicating whether the group has a visible title.
    /// </summary>
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
}
