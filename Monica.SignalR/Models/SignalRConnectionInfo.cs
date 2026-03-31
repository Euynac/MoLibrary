using System.Collections.Concurrent;
using System.Security.Claims;

namespace Monica.SignalR.Models;

/// <summary>
/// Represents a live SignalR connection tracked by the server.
/// </summary>
public sealed class SignalRConnectionInfo
{
    /// <summary>
    /// Gets or sets the SignalR connection identifier.
    /// </summary>
    public required string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the user principal attached to the connection.
    /// </summary>
    public required ClaimsPrincipal ClaimsPrincipal { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the connection was first tracked.
    /// </summary>
    public DateTime ConnectionTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the per-connection state bag.
    /// </summary>
    public ConcurrentDictionary<string, object?> StateBag { get; } = new();

    /// <summary>
    /// Stores a typed state value on the connection.
    /// </summary>
    public void SaveState<T>(T state)
    {
        var key = typeof(T).FullName ?? typeof(T).Name;
        StateBag.AddOrUpdate(key, state, (_, _) => state);
    }

    /// <summary>
    /// Retrieves a typed state value from the connection.
    /// </summary>
    public T? GetState<T>()
    {
        var key = typeof(T).FullName ?? typeof(T).Name;
        return StateBag.TryGetValue(key, out var value) && value is T typedValue ? typedValue : default;
    }
}
