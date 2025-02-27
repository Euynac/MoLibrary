using System.Collections.Concurrent;
using System.Security.Claims;

namespace BuildingBlocksPlatform.SignalR;

public interface IMoSignalRConnectionManager
{
    /// <summary>
    ///     增加连接
    /// </summary>
    /// <param name="connectionId"></param>
    /// <param name="cp"></param>
    void AddConnection(string connectionId, ClaimsPrincipal cp);

    /// <summary>
    ///     移除连接
    /// </summary>
    /// <param name="connectionId"></param>
    void RemoveConnection(string connectionId);

    /// <summary>
    ///     获取所有正在连接的信息
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<SignalRConnectionInfo> GetConnectionInfos();

    /// <summary>
    ///     获取SignalR连接信息
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
        return [.. _connections.Values];
    }

    public SignalRConnectionInfo? GetConnectionInfo(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var info) ? info : null;
    }
}

public class SignalRConnectionInfo
{
    public required string ConnectionId { get; set; }
    public required ClaimsPrincipal ClaimsPrincipal { get; set; }
    public DateTime ConnectionTime { get; set; } = DateTime.Now;
    public ConcurrentDictionary<string, object?> Dictionary { get; set; } = [];

    /// <summary>
    ///     保存用户状态
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="state"></param>
    public void SaveState<T>(T state)
    {
        Dictionary.AddOrUpdate(typeof(T).Name, f => state, (s, o) => state);
    }

    /// <summary>
    ///     获取用户状态
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? GetState<T>()
    {
        if (Dictionary.TryGetValue(typeof(T).Name, out var value)) return (T?)value;
        return default;
    }
}