using MoLibrary.StateStore.QueryBuilder.Interfaces;
using MoLibrary.StateStore.QueryBuilder;

namespace MoLibrary.StateStore;

/// <summary>
/// 状态存储接口，提供状态的增删改查功能
/// </summary>
public interface IDistributedStateStore : IMoStateStore
{
    /// <summary>
    /// 批量获取字符串类型的状态数据，不使用键前缀
    /// </summary>
    /// <param name="keys">状态键列表</param>
    /// <param name="removePrefix">返回时是否自动移除Key前缀</param>
    /// <param name="removeEmptyValue">是否移除空值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回状态字典，获取失败返回空字典</returns>
    Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取字符串类型的状态数据，使用指定的键前缀
    /// </summary>
    /// <param name="keys">状态键列表</param>
    /// <param name="prefix">键前缀</param>
    /// <param name="removePrefix">返回时是否自动移除Key前缀</param>
    /// <param name="removeEmptyValue">是否移除空值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回状态字典，获取失败返回空字典</returns>
    Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys, string? prefix,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 查询所有满足给定条件的状态
    /// </summary>
    /// <typeparam name="T">状态数据类型</typeparam>
    /// <param name="query">查询构建器函数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回满足条件的状态字典，键为状态键，值为状态数据</returns>
    Task<Dictionary<string, T?>> QueryStateAsync<T>(Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 获取字符串类型的单个状态原始数据，不使用键前缀
    /// </summary>
    /// <param name="key">状态键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回状态数据，获取失败返回null</returns>
    Task<string?> GetStateAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取字符串类型的单个状态原始数据，使用指定的键前缀
    /// </summary>
    /// <param name="key">状态键</param>
    /// <param name="prefix">键前缀</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回状态数据，获取失败返回null</returns>
    Task<string?> GetStateAsync(string key, string? prefix, CancellationToken cancellationToken = default);
}