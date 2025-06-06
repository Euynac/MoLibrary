namespace MoLibrary.StateStore.CancellationManager;

/// <summary>
/// 分布式取消令牌管理器接口
/// 提供跨微服务实例的取消令牌创建、取消和监听功能
/// </summary>
public interface IMoCancellationManager
{
    /// <summary>
    /// 创建或获取指定键的分布式取消令牌
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    /// <returns>返回与指定键关联的取消令牌</returns>
    Task<CancellationToken> GetOrCreateTokenAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消指定键的分布式取消令牌
    /// 这将触发所有监听此键的微服务实例中的取消令牌
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    Task CancelTokenAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查指定键的取消令牌是否已被取消
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    /// <returns>如果已取消返回true，否则返回false</returns>
    Task<bool> IsCancelledAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重置指定键的取消令牌状态
    /// 将已取消的令牌重置为未取消状态，允许重新使用
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    Task ResetTokenAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定键的取消令牌
    /// 清理不再需要的取消令牌资源
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    Task DeleteTokenAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有活动取消令牌的键列表
    /// </summary>
    /// <param name="cancellationToken">操作的取消令牌</param>
    /// <returns>返回所有活动取消令牌的键列表</returns>
    Task<IReadOnlyList<string>> GetActiveTokenKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量取消多个取消令牌
    /// </summary>
    /// <param name="keys">要取消的取消令牌键列表</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    Task CancelTokensAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default);
} 