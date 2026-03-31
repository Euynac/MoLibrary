using System.Collections.Concurrent;
using System.Security.Claims;
using Monica.SignalR.Abstractions;
using Monica.SignalR.Models;

namespace Monica.SignalR.Services.Support;

/// <summary>
/// Default in-memory implementation of <see cref="ISignalRConnectionRegistry"/>.
/// </summary>
internal sealed class SignalRConnectionRegistry : ISignalRConnectionRegistry
{
    private readonly ConcurrentDictionary<string, SignalRConnectionInfo> _connections = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void AddConnection(string connectionId, ClaimsPrincipal claimsPrincipal)
    {
        _connections[connectionId] = new SignalRConnectionInfo
        {
            ConnectionId = connectionId,
            ClaimsPrincipal = claimsPrincipal
        };
    }

    /// <inheritdoc />
    public void RemoveConnection(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
    }

    /// <inheritdoc />
    public IReadOnlyList<SignalRConnectionInfo> GetConnectionInfos()
    {
        return _connections.Values.ToList();
    }

    /// <inheritdoc />
    public SignalRConnectionInfo? GetConnectionInfo(string connectionId)
    {
        return _connections.GetValueOrDefault(connectionId);
    }
}
