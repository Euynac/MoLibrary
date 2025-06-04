namespace MoLibrary.StateStore.CancellationManager;

/// <summary>
/// 分布式取消令牌状态数据模型
/// </summary>
public class DistributedCancellationTokenState
{
    /// <summary>
    /// 取消令牌键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 是否已取消
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 版本号，用于乐观锁控制
    /// </summary>
    public long Version { get; set; } = 1;
}