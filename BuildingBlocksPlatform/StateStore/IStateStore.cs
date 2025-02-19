using BuildingBlocksPlatform.StateStore.QueryBuilder.Interfaces;
using BuildingBlocksPlatform.StateStore.QueryBuilder;

namespace BuildingBlocksPlatform.StateStore;

public interface IStateStore
{
    /// <summary>
    /// 查询所有满足给定条件的状态
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Dictionary<string, T>> QueryStateAsync<T>(Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 判断状态是否存在
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="prefix">键前缀，一般使用nameof获取模型名</param>
    /// <param name="cancellationToken"></param>
    /// <returns>获取失败返回null</returns>
    Task<bool> ExistAsync<T>(string key, string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取状态
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="keys"></param>
    /// <param name="prefix">键前缀，一般使用nameof获取模型名</param>
    /// <param name="removePrefix">返回时自动移除Key前缀</param>
    /// <param name="removeEmptyValue"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>获取失败返回空字典</returns>
    Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys, string? prefix = null,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取状态
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="prefix">键前缀，一般使用nameof获取模型名</param>
    /// <param name="removePrefix">返回时自动移除Key前缀</param>
    /// <param name="removeEmptyValue"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>获取失败返回空字典</returns>
    Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys, string? prefix = null,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取状态
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="prefix">键前缀，一般使用nameof获取模型名</param>
    /// <param name="cancellationToken"></param>
    /// <returns>获取失败返回null</returns>
    Task<T?> GetStateAsync<T>(string key, string? prefix = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// 获取状态
    /// </summary>
    /// <param name="key"></param>
    /// <param name="prefix">键前缀，一般使用nameof获取模型名</param>
    /// <param name="cancellationToken"></param>
    /// <returns>获取失败返回null</returns>
    Task<string?> GetStateAsync(string key, string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存状态
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="prefix">键前缀，一般使用nameof获取模型名</param>
    /// <param name="cancellationToken"></param>
    /// <param name="ttl">消息自动清除时间，填入0代表永久存储，不受全局设置影响</param>
    /// <returns>保存失败抛出异常</returns>
    Task SaveStateAsync(string key, object value, string? prefix = null,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null);

    /// <summary>
    /// 删除状态
    /// </summary>
    /// <param name="key"></param>
    /// <param name="prefix">键前缀，一般使用nameof获取模型名</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeleteStateAsync(string key, string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除状态
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="prefix">键前缀，一般使用nameof获取模型名</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeleteBulkStateAsync(IReadOnlyList<string> keys, string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除状态
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="prefix">键前缀，一般使用nameof获取模型名</param>
    /// <param name="cancellationToken"></param>
    Task<(T value, string etag)> GetVersionAsync<T>(string key, string? prefix = null,
     CancellationToken cancellationToken = default);
}