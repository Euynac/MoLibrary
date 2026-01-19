using MoLibrary.RegisterCentre.Models;

namespace MoLibrary.RegisterCentre.Interfaces;

/// <summary>
/// 注册结果
/// </summary>
public record RegistrationResult(bool Success, DateTime? HeartbeatTime, string? ErrorMessage);

/// <summary>
/// 注册状态管理接口
/// </summary>
public interface IRegistrationStateManager
{
    /// <summary>
    /// 注册或心跳（初次注册也是心跳，因为 inst_id 不会重复）
    /// </summary>
    Task<RegistrationResult> RegisterOrHeartbeatAsync(CancellationToken ct = default);

    /// <summary>
    /// 检查 Leader Key 是否存在
    /// </summary>
    Task<bool> LeaderExistsAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取当前 Leader 状态
    /// </summary>
    Task<LeaderState?> GetLeaderStateAsync(CancellationToken ct = default);

    /// <summary>
    /// 尝试成为 Leader（仅当 Leader Key 不存在时）
    /// </summary>
    /// <returns>成功时返回 (true, LeaderState)，失败返回 (false, null)</returns>
    Task<(bool Success, LeaderState? State, string? ETag)> TryBecomeLeaderAsync(CancellationToken ct = default);

    /// <summary>
    /// 续约 Leader（使用 ETag 验证）
    /// </summary>
    /// <param name="expectedETag">预期的 ETag</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>
    /// 成功时返回 (true, NewETag, null, null)
    /// 失败时返回 (false, null, ActualState, ActualETag) - 包含当前 StateStore 中的实际状态和 ETag
    /// </returns>
    Task<(bool Success, string? NewETag, LeaderState? ActualState, string? ActualETag)> RenewLeaderLeaseAsync(string expectedETag, CancellationToken ct = default);

    /// <summary>
    /// 删除 Leader Key（优雅关闭时使用）
    /// </summary>
    Task DeleteLeaderKeyAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取所有已注册的实例状态
    /// </summary>
    Task<List<InstanceState>> GetAllInstancesAsync(CancellationToken ct = default);
    /// <summary>
    /// 获取所有Leader实例状态
    /// </summary>
    Task<List<InstanceState>> GetAllLeaderInstancesAsync(CancellationToken ct = default);

    /// <summary>
    /// 强制删除指定服务的 Leader Key（用于管理/调试）
    /// </summary>
    Task ForceDeleteLeaderKeyAsync(string serviceName, CancellationToken ct = default);
}
