using Monica.ServiceDiscovery.Events;
using Monica.ServiceDiscovery.Models;

namespace Monica.ServiceDiscovery.Abstractions;

/// <summary>
/// Leader 选举服务接口
/// </summary>
public interface ILeaderElectionService
{
    /// <summary>
    /// 当前 Leader 状态
    /// </summary>
    LeaderStatus CurrentStatus { get; }

    /// <summary>
    /// 是否是 Leader
    /// </summary>
    bool IsLeader { get; }

    /// <summary>
    /// 成为 Leader 的时间（如果不是 Leader 则为 null）
    /// </summary>
    DateTime? LeaderBecomeTime { get; }

    /// <summary>
    /// 当前 ETag（用于续约验证）
    /// </summary>
    string? CurrentETag { get; }

    /// <summary>
    /// Leader 获得事件
    /// </summary>
    event EventHandler<LeaderGainedEvent>? OnLeaderGained;

    /// <summary>
    /// Leader 丢失事件
    /// </summary>
    event EventHandler<LeaderLostEvent>? OnLeaderLost;

    /// <summary>
    /// 设置为 Leader
    /// </summary>
    void SetAsLeader(DateTime becomeTime, string eTag);

    /// <summary>
    /// 更新 ETag
    /// </summary>
    void UpdateETag(string newETag);

    /// <summary>
    /// 触发 Leader 丢失
    /// </summary>
    void TriggerLeaderLost(LeaderLostReason reason);

    /// <summary>
    /// 重置 Leader 状态
    /// </summary>
    void ResetLeaderState();
}
