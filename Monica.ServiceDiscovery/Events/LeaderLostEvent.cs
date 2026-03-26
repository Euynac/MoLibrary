namespace Monica.ServiceDiscovery.Events;

/// <summary>
/// Leader 丢失原因
/// </summary>
public enum LeaderLostReason
{
    /// <summary>
    /// 网络隔离
    /// </summary>
    NetworkIsolation,

    /// <summary>
    /// Leader Key 被其他实例占用
    /// </summary>
    LeaderKeyTakenByOther,

    /// <summary>
    /// 优雅关闭
    /// </summary>
    GracefulShutdown,

    /// <summary>
    /// 挣扎超时
    /// </summary>
    StruggleTimeout,

    /// <summary>
    /// Leader Key 已过期或被删除
    /// </summary>
    LeaderKeyExpired
}

/// <summary>
/// Leader 丢失事件
/// </summary>
public class LeaderLostEvent
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
    /// 丢失 Leader 的时间
    /// </summary>
    public required DateTime LostTime { get; init; }

    /// <summary>
    /// 丢失原因
    /// </summary>
    public required LeaderLostReason Reason { get; init; }
}
