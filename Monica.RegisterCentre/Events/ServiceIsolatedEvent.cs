namespace Monica.RegisterCentre.Events;

/// <summary>
/// 服务孤立事件
/// </summary>
public class ServiceIsolatedEvent
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
    /// 检测到孤立的时间
    /// </summary>
    public required DateTime IsolationDetectedTime { get; init; }

    /// <summary>
    /// 最后成功心跳时间
    /// </summary>
    public DateTime? LastSuccessfulHeartbeat { get; init; }
}
