namespace Monica.ServiceDiscovery.Models;

/// <summary>
/// Describes a service domain in the discovery catalog.
/// </summary>
public sealed class DomainInfo
{
    /// <summary>
    /// Gets or sets the stable domain identifier.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the display name shown to users.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the optional domain description.
    /// </summary>
    public string? Description { get; set; }
}
