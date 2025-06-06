using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MoLibrary.StateStore.CancellationManager;

/// <summary>
/// 内存版取消令牌管理器实现
/// 使用内存存储和事件机制，无需轮询，提供即时响应
/// </summary>
/// <remarks>
/// 注意：此实现仅适用于单进程/单实例场景，不支持跨进程的分布式取消
/// </remarks>
/// <param name="logger">日志记录器</param>
public class InMemoryCancellationManager(ILogger<InMemoryCancellationManager> logger) : IMoCancellationManager
{
    /// <summary>
    /// 内存中的取消令牌状态存储
    /// </summary>
    private readonly ConcurrentDictionary<string, InMemoryTokenState> _tokenStates = new();
    
    /// <summary>
    /// 本地取消令牌源缓存
    /// </summary>
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _tokenSources = new();

    /// <summary>
    /// 创建或获取指定键的取消令牌
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    /// <returns>返回与指定键关联的取消令牌</returns>
    public Task<CancellationToken> GetOrCreateTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        // 获取或创建令牌状态
        var tokenState = _tokenStates.GetOrAdd(key, k => new InMemoryTokenState
        {
            Key = k,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        });

        // 获取或创建取消令牌源
        var tokenSource = _tokenSources.GetOrAdd(key, k =>
        {
            var source = new CancellationTokenSource();
            
            // 如果状态已被取消，立即取消新创建的令牌源
            if (tokenState.IsCancelled)
            {
                source.Cancel();
                logger.LogDebug("Immediately cancelled token for key: {Key} due to existing cancelled state", key);
            }
            
            logger.LogDebug("Created new cancellation token for key: {Key}", key);
            return source;
        });

        return Task.FromResult(tokenSource.Token);
    }

    /// <summary>
    /// 取消指定键的取消令牌
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    public Task CancelTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        // 更新状态
        var tokenState = _tokenStates.GetOrAdd(key, k => new InMemoryTokenState
        {
            Key = k,
            CreatedAt = DateTime.UtcNow
        });

        tokenState.IsCancelled = true;
        tokenState.LastUpdatedAt = DateTime.UtcNow;
        tokenState.Version++;

        // 取消本地令牌源（如果存在）
        if (_tokenSources.TryGetValue(key, out var tokenSource))
        {
            if (!tokenSource.Token.IsCancellationRequested)
            {
                tokenSource.Cancel();
                logger.LogInformation("Cancelled token for key: {Key}", key);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 检查指定键的取消令牌是否已被取消
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    /// <returns>如果已取消返回true，否则返回false</returns>
    public Task<bool> IsCancelledAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        var isCancelled = _tokenStates.TryGetValue(key, out var state) && state.IsCancelled;
        return Task.FromResult(isCancelled);
    }

    /// <summary>
    /// 重置指定键的取消令牌状态
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    public Task ResetTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        // 重置状态
        var tokenState = _tokenStates.GetOrAdd(key, k => new InMemoryTokenState
        {
            Key = k,
            CreatedAt = DateTime.UtcNow
        });

        tokenState.IsCancelled = false;
        tokenState.LastUpdatedAt = DateTime.UtcNow;
        tokenState.Version++;

        // 移除并重新创建取消令牌源
        if (_tokenSources.TryRemove(key, out var oldTokenSource))
        {
            oldTokenSource.Dispose();
        }

        var newTokenSource = new CancellationTokenSource();
        _tokenSources.TryAdd(key, newTokenSource);

        logger.LogInformation("Reset token for key: {Key}", key);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 删除指定键的取消令牌
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    public Task DeleteTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        // 移除状态
        _tokenStates.TryRemove(key, out _);

        // 移除并清理取消令牌源
        if (_tokenSources.TryRemove(key, out var tokenSource))
        {
            if (!tokenSource.Token.IsCancellationRequested)
            {
                tokenSource.Cancel();
            }
            tokenSource.Dispose();
        }

        logger.LogInformation("Deleted token for key: {Key}", key);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取所有活动取消令牌的键列表
    /// </summary>
    /// <param name="cancellationToken">操作的取消令牌</param>
    /// <returns>返回所有活动取消令牌的键列表</returns>
    public Task<IReadOnlyList<string>> GetActiveTokenKeysAsync(CancellationToken cancellationToken = default)
    {
        var activeKeys = _tokenStates
            .Where(kvp => !kvp.Value.IsCancelled)
            .Select(kvp => kvp.Key)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(activeKeys);
    }

    /// <summary>
    /// 批量取消多个取消令牌
    /// </summary>
    /// <param name="keys">要取消的取消令牌键列表</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    public async Task CancelTokensAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys == null || keys.Count == 0)
            return;

        var cancelTasks = keys.Select(key => CancelTokenAsync(key, cancellationToken));
        await Task.WhenAll(cancelTasks);

        logger.LogInformation("Cancelled {Count} tokens", keys.Count);
    }

    /// <summary>
    /// 内存令牌状态数据模型
    /// </summary>
    private class InMemoryTokenState
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
        /// 版本号，用于状态变更追踪
        /// </summary>
        public long Version { get; set; } = 1;
    }
} 