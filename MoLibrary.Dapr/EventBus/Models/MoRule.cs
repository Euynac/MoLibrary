namespace MoLibrary.Dapr.EventBus.Models;

/// <summary>
/// This class defines the rule for subscribe endpoint.
/// </summary>
internal abstract class MoRule
{
    /// <summary>
    /// Gets or sets the CEL expression to match this route.
    /// </summary>
    public string Match { get; set; } = default!;

    /// <summary>
    /// Gets or sets the path of the route.
    /// </summary>
    public string Path { get; set; } = default!;
}