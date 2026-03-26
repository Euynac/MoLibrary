namespace Monica.ServiceDiscovery.Events;

/// <summary>
/// Leader 获得事件
/// </summary>
public class LeaderGainedEvent
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// 实例 ID
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// 成为 Leader 的时间
    /// </summary>
    public required DateTime BecomeLeaderTime { get; init; }
}
