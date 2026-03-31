namespace Monica.SignalR.Models;

/// <summary>
/// Represents a connected SignalR user surfaced by inspection APIs.
/// </summary>
public sealed class SignalRConnectedUserInfo
{
    /// <summary>
    /// Gets or sets the SignalR connection identifier.
    /// </summary>
    public required string ConnectionId { get; init; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the connection was first tracked.
    /// </summary>
    public DateTime ConnectionTime { get; init; }

    /// <summary>
    /// Gets or sets the flattened claims dictionary for display purposes.
    /// </summary>
    public Dictionary<string, string> Claims { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the connection is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// Gets or sets the resolved user name.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// Gets or sets the resolved application user identifier.
    /// </summary>
    public string? UserId { get; init; }
}
