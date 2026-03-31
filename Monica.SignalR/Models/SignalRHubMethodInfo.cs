namespace Monica.SignalR.Models;

/// <summary>
/// Describes a server-side SignalR hub method.
/// </summary>
public sealed class SignalRHubMethodInfo
{
    /// <summary>
    /// Gets or sets the friendly method description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the method name exposed by the hub.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the method parameter metadata.
    /// </summary>
    public List<SignalRHubParameterInfo> Parameters { get; init; } = [];
}
