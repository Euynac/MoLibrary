namespace Monica.ServiceDiscovery.Models;

/// <summary>
/// Describes a predefined service entry exposed by the discovery catalog.
/// </summary>
public sealed class PredefinedServiceInfo
{
    /// <summary>
    /// Gets or sets the stable application identifier.
    /// </summary>
    public required string AppId { get; set; }

    /// <summary>
    /// Gets or sets the user-facing application name.
    /// </summary>
    public string? AppName { get; set; }
}
