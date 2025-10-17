using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Extensions;
using MoLibrary.StateStore.Modules;
// ReSharper disable PossiblyMistakenUseOfCancellationToken

namespace MoLibrary.StateStore.CancellationManager;

//TODO 是不是应该用分布式锁
//TODO 使用EventBus替代轮询
//TODO 替代InMemoryCancellationManager

/// <summary>
/// 默认分布式取消令牌管理器实现
/// 使用 IStateStore 作为底层存储，实现跨微服务实例的取消令牌管理
/// </summary>
/// <remarks>
/// 初始化默认分布式取消令牌管理器
/// </remarks>
/// <param name="stateStore">状态存储服务</param>
/// <param name="logger">日志记录器</param>
/// <param name="options">配置选项</param>
public class DistributedCancellationManager(
    IMoStateStore stateStore,
    ILogger<DistributedCancellationManager> logger,
    IOptions<ModuleCancellationManagerOption> options) : IMoCancellationManager
{
    private const string StateKeyPrefix = "DistributedCancellation";

    private readonly ModuleCancellationManagerOption _options = options.Value;
    
    /// <summary>
    /// 本地取消令牌源缓存，避免重复轮询状态存储
    /// </summary>
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _localTokenSources = new();
    
    /// <summary>
    /// 轮询任务缓存，每个键对应一个后台轮询任务
    /// </summary>
    private readonly ConcurrentDictionary<string, Task> _pollingTasks = new();

    /// <summary>
    /// 创建或获取指定键的分布式取消令牌
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    /// <returns>返回与指定键关联的取消令牌</returns>
    public async Task<CancellationToken> GetOrCreateTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        // 如果本地已存在且未取消，直接返回
        if (_localTokenSources.TryGetValue(key, out var existingSource) && !existingSource.Token.IsCancellationRequested)
        {
            if (_options.EnableVerboseLogging)
                logger.LogDebug("Returning existing local cancellation token for key: {Key}", key);
            return existingSource.Token;
        }

        // 创建新的本地取消令牌源
        var tokenSource = new CancellationTokenSource();
        _localTokenSources.AddOrUpdate(key, tokenSource, (_, _) => tokenSource);

        // 初始化或获取分布式状态
        var state = await stateStore.GetStateAsync<DistributedCancellationTokenState>(key, StateKeyPrefix, cancellationToken);
        if (state == null)
        {
            // 创建新的分布式状态
            state = new DistributedCancellationTokenState
            {
                Key = key,
                IsCancelled = false,
                CreatedAt = DateTime.Now,
                LastUpdatedAt = DateTime.Now,
                Version = 1
            };
            
            await stateStore.SaveStateAsync(key, state, StateKeyPrefix, cancellationToken, _options.StateTtl);
            logger.LogInformation("Created new distributed cancellation token for key: {Key}", key);
        }

        // 如果分布式状态已取消，立即取消本地令牌
        if (state.IsCancelled)
        {
            await tokenSource.CancelAsync();
            if (_options.EnableVerboseLogging)
                logger.LogDebug("Local token immediately cancelled due to distributed state for key: {Key}", key);
        }

        // 启动后台轮询任务监听分布式状态变化
        StartPollingTask(key);

        return tokenSource.Token;
    }

    /// <summary>
    /// 取消指定键的分布式取消令牌
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    public async Task CancelTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        try
        {
            // 更新分布式状态
            var state = await stateStore.GetStateAsync<DistributedCancellationTokenState>(key, StateKeyPrefix, cancellationToken);
            if (state != null)
            {
                state.IsCancelled = true;
                state.LastUpdatedAt = DateTime.Now;
                state.Version++;
                
                await stateStore.SaveStateAsync(key, state, StateKeyPrefix, cancellationToken, _options.StateTtl);
                logger.LogInformation("Cancelled distributed cancellation token for key: {Key}", key);
            }

            // 取消本地令牌
            if (_localTokenSources.TryGetValue(key, out var tokenSource))
            {
                await tokenSource.CancelAsync();
            }
        }
        catch (Exception ex)
        {
            throw ex.CreateException(logger, "Failed to cancel distributed cancellation token for key: {0}", key);
        }
    }

    /// <summary>
    /// 检查指定键的取消令牌是否已被取消
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    /// <returns>如果已取消返回true，否则返回false</returns>
    public async Task<bool> IsCancelledAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        var state = await stateStore.GetStateAsync<DistributedCancellationTokenState>(key, StateKeyPrefix, cancellationToken);
        return state?.IsCancelled ?? false;
    }

    /// <summary>
    /// 重置指定键的取消令牌状态
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    public async Task ResetTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        try
        {
            // 重置分布式状态
            var state = await stateStore.GetStateAsync<DistributedCancellationTokenState>(key, StateKeyPrefix, cancellationToken);
            if (state != null)
            {
                state.IsCancelled = false;
                state.LastUpdatedAt = DateTime.Now;
                state.Version++;
                
                await stateStore.SaveStateAsync(key, state, StateKeyPrefix, cancellationToken, _options.StateTtl);
            }

            // 重置本地令牌源
            var newTokenSource = new CancellationTokenSource();
            _localTokenSources.AddOrUpdate(key, newTokenSource, (_, _) => newTokenSource);

            // 重新启动轮询任务
            StartPollingTask(key);

            logger.LogInformation("Reset distributed cancellation token for key: {Key}", key);
        }
        catch (Exception ex)
        {
            throw ex.CreateException(logger, "Failed to reset distributed cancellation token for key: {0}", key);
        }
    }

    /// <summary>
    /// 删除指定键的取消令牌
    /// </summary>
    /// <param name="key">取消令牌的唯一标识键</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    public async Task DeleteTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        try
        {
            // 删除分布式状态
            await stateStore.DeleteStateAsync(key, StateKeyPrefix, cancellationToken);

            // 清理本地资源
            if (_localTokenSources.TryRemove(key, out var tokenSource))
            {
                await tokenSource.CancelAsync();
                tokenSource.Dispose();
            }

            logger.LogInformation("Deleted distributed cancellation token for key: {Key}", key);
        }
        catch (Exception ex)
        {
            throw ex.CreateException(logger, "Failed to delete distributed cancellation token for key: {0}", key);
        }
    }

    /// <summary>
    /// 获取所有活动取消令牌的键列表
    /// </summary>
    /// <param name="cancellationToken">操作的取消令牌</param>
    /// <returns>返回所有活动取消令牌的键列表</returns>
    public async Task<IReadOnlyList<string>> GetActiveTokenKeysAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 由于StateStore的QueryBuilder不支持通用的前缀查询，我们需要通过其他方式获取所有状态
            // 这里采用简化的实现，实际使用中可能需要根据具体的IStateStore实现调整
            var result = new List<string>();
            
            // 这是一个权宜之计，实际生产环境中应该通过更高效的方式实现
            // 例如：维护一个活动token键的索引，或者使用支持前缀查询的存储实现
            logger.LogWarning("GetActiveTokenKeysAsync is using a simplified implementation. " +
                             "Consider implementing a more efficient solution for production use.");
            
            // 返回当前本地缓存中的活动令牌键
            foreach (var kvp in _localTokenSources)
            {
                if (!kvp.Value.Token.IsCancellationRequested)
                {
                    // 双重检查分布式状态
                    var state = await stateStore.GetStateAsync<DistributedCancellationTokenState>(kvp.Key, StateKeyPrefix, cancellationToken);
                    if (state != null && !state.IsCancelled)
                    {
                        result.Add(kvp.Key);
                    }
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            throw ex.CreateException(logger, "Failed to get active token keys");
        }
    }

    /// <summary>
    /// 批量取消多个取消令牌
    /// </summary>
    /// <param name="keys">要取消的取消令牌键列表</param>
    /// <param name="cancellationToken">操作的取消令牌</param>
    public async Task CancelTokensAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys == null || !keys.Any())
            return;

        var tasks = keys.Select(key => CancelTokenAsync(key, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 启动后台轮询任务监听分布式状态变化
    /// </summary>
    /// <param name="key">取消令牌键</param>
    private void StartPollingTask(string key)
    {
        if (_pollingTasks.ContainsKey(key))
            return;

        var pollingTask = Task.Run(async () =>
        {
            var pollingIntervalMs = _options.PollingIntervalMs;
            var lastVersion = 0L;

            if (_options.EnableVerboseLogging)
                logger.LogDebug("Started polling task for cancellation token key: {Key} with interval {IntervalMs}ms", key, pollingIntervalMs);

            while (_localTokenSources.TryGetValue(key, out var tokenSource) && !tokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var state = await stateStore.GetStateAsync<DistributedCancellationTokenState>(key, StateKeyPrefix);
                    if (state == null)
                    {
                        // 分布式状态已被删除，清理本地资源
                        if (_localTokenSources.TryRemove(key, out var localSource))
                        {
                            await localSource.CancelAsync();
                            localSource.Dispose();
                        }
                        if (_options.EnableVerboseLogging)
                            logger.LogDebug("Distributed state deleted, cleaned up local resources for key: {Key}", key);
                        break;
                    }

                    // 检查版本变化和取消状态
                    if (state.Version > lastVersion)
                    {
                        lastVersion = state.Version;
                        
                        if (state.IsCancelled && !tokenSource.Token.IsCancellationRequested)
                        {
                            await tokenSource.CancelAsync();
                            logger.LogDebug("Local cancellation token cancelled due to distributed state change for key: {Key}", key);
                            break;
                        }
                    }

                    await Task.Delay(pollingIntervalMs, tokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，退出轮询
                    if (_options.EnableVerboseLogging)
                        logger.LogDebug("Polling task cancelled for key: {Key}", key);
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error occurred during polling for key: {Key}", key);
                    await Task.Delay(pollingIntervalMs);
                }
            }

            // 清理轮询任务
            _pollingTasks.TryRemove(key, out _);
            if (_options.EnableVerboseLogging)
                logger.LogDebug("Polling task completed for key: {Key}", key);
        });

        _pollingTasks.TryAdd(key, pollingTask);
    }

    /// <summary>
    /// 分布式取消令牌状态数据模型
    /// </summary>
    private class DistributedCancellationTokenState
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
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 版本号，用于乐观锁控制
        /// </summary>
        public long Version { get; set; } = 1;
    }
}


