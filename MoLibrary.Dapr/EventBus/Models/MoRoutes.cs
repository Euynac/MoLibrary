namespace MoLibrary.Dapr.EventBus.Models;

/// <summary>
/// This class defines the routes for subscribe endpoint.
/// </summary>
internal abstract class MoRoutes
{
    /// <summary>
    /// Gets or sets the default route
    /// </summary>
    public string? Default { get; set; }

    /// <summary>
    /// Gets or sets the routing rules
    /// </summary>
    public List<MoRule>? Rules { get; set; }
}