using System.Security.Claims;
using Monica.SignalR.Models;

namespace Monica.SignalR.Abstractions;

/// <summary>
/// Tracks live SignalR connections for diagnostics and server-side fan-out helpers.
/// </summary>
public interface ISignalRConnectionRegistry
{
    /// <summary>
    /// Adds a new authenticated connection to the registry.
    /// </summary>
    /// <param name="connectionId">The SignalR connection identifier.</param>
    /// <param name="claimsPrincipal">The authenticated user principal.</param>
    void AddConnection(string connectionId, ClaimsPrincipal claimsPrincipal);

    /// <summary>
    /// Removes a disconnected connection from the registry.
    /// </summary>
    /// <param name="connectionId">The SignalR connection identifier.</param>
    void RemoveConnection(string connectionId);

    /// <summary>
    /// Returns a snapshot of all currently tracked connections.
    /// </summary>
    IReadOnlyList<SignalRConnectionInfo> GetConnectionInfos();

    /// <summary>
    /// Gets a tracked connection by its SignalR connection identifier.
    /// </summary>
    /// <param name="connectionId">The SignalR connection identifier.</param>
    SignalRConnectionInfo? GetConnectionInfo(string connectionId);
}
