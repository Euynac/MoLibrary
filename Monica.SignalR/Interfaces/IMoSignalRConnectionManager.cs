using System.Collections.Concurrent;
using System.Security.Claims;

namespace Monica.SignalR.Interfaces;

public interface IMoSignalRConnectionManager
{
    /// <summary>
    /// increase connection
    /// </summary>
    /// <param name="connectionId"></param>
    /// <param name="cp"></param>
    void AddConnection(string connectionId, ClaimsPrincipal cp);

    /// <summary>
    /// Remove connection
    /// </summary>
    /// <param name="connectionId"></param>
    void RemoveConnection(string connectionId);

    /// <summary>
    /// Get information about all connections
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<SignalRConnectionInfo> GetConnectionInfos();

    /// <summary>
    /// Get SignalR connection information
    /// </summary>
    /// <returns></returns>
    SignalRConnectionInfo? GetConnectionInfo(string connectionId);
}

public class MoSignalRConnectionManager : IMoSignalRConnectionManager
{
    private readonly ConcurrentDictionary<string, SignalRConnectionInfo> _connections = new();

    public void AddConnection(string connectionId, ClaimsPrincipal cp)
    {
        _connections.TryAdd(connectionId, new SignalRConnectionInfo
        {
            ConnectionId = connectionId,
            ClaimsPrincipal = cp
        });
    }

    public void RemoveConnection(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
    }

    public IReadOnlyList<SignalRConnectionInfo> GetConnectionInfos()
    {
        return _connections.Values.ToList();
    }

    public SignalRConnectionInfo? GetConnectionInfo(string connectionId)
    {
        return _connections.GetValueOrDefault(connectionId);
    }
}

public class SignalRConnectionInfo
{
    public required string ConnectionId { get; set; }
    public required ClaimsPrincipal ClaimsPrincipal { get; set; }
    public DateTime ConnectionTime { get; set; } = DateTime.Now;
    public ConcurrentDictionary<string, object?> Dictionary { get; set; } = [];

    /// <summary>
    /// Save user status
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="state"></param>
    public void SaveState<T>(T state)
    {
        Dictionary.AddOrUpdate(typeof(T).Name, f => state, (s, o) => state);
    }

    /// <summary>
    /// Get user status
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? GetState<T>()
    {
        if (Dictionary.TryGetValue(typeof(T).Name, out var value)) return (T?)value;
        return default;
    }
}