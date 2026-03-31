namespace Monica.SignalR.Models;

/// <summary>
/// Describes a mapped SignalR hub and its callable methods.
/// </summary>
public sealed class SignalRHubInfo
{
    /// <summary>
    /// Gets or sets the hub type name.
    /// </summary>
    public required string HubName { get; init; }

    /// <summary>
    /// Gets or sets the mapped route.
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// Gets or sets the callable methods declared on the hub.
    /// </summary>
    public List<SignalRHubMethodInfo> Methods { get; init; } = [];
}
