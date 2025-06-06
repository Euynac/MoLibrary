namespace MoLibrary.StateStore;


public interface IMoStateStore
{
    /// <summary>
    /// 判断指定键的状态是否存在，使用泛型T的类型名作为键前缀
    /// </summary>
    /// <typeparam name="T">状态数据类型</typeparam>
    /// <param name="key">状态键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>存在返回true，不存在返回false</returns>
    Task<bool> ExistAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断指定键的状态是否存在，使用指定的键前缀
    /// </summary>
    /// <typeparam name="T">状态数据类型</typeparam>
    /// <param name="key">状态键</param>
    /// <param name="prefix">键前缀</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>存在返回true，不存在返回false</returns>
    Task<bool> ExistAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取指定类型的状态数据，使用泛型T的类型名作为键前缀
    /// </summary>
    /// <typeparam name="T">状态数据类型</typeparam>
    /// <param name="keys">状态键列表</param>
    /// <param name="removePrefix">返回时是否自动移除Key前缀</param>
    /// <param name="removeEmptyValue">是否移除空值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回状态字典，获取失败返回空字典</returns>
    Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取指定类型的状态数据，使用指定的键前缀
    /// </summary>
    /// <typeparam name="T">状态数据类型</typeparam>
    /// <param name="keys">状态键列表</param>
    /// <param name="prefix">键前缀</param>
    /// <param name="removePrefix">返回时是否自动移除Key前缀</param>
    /// <param name="removeEmptyValue">是否移除空值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回状态字典，获取失败返回空字典</returns>
    Task<Dictionary<string, T?>> GetBulkStateAsync<T>(IReadOnlyList<string> keys, string? prefix,
        bool removePrefix = true,
        bool removeEmptyValue = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定类型的单个状态数据，使用泛型T的类型名作为键前缀
    /// </summary>
    /// <typeparam name="T">状态数据类型</typeparam>
    /// <param name="key">状态键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回状态数据，获取失败返回null</returns>
    Task<T?> GetStateAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定类型的单个状态数据，使用指定的键前缀
    /// </summary>
    /// <typeparam name="T">状态数据类型</typeparam>
    /// <param name="key">状态键</param>
    /// <param name="prefix">键前缀</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回状态数据，获取失败返回null</returns>
    Task<T?> GetStateAsync<T>(string key, string? prefix, CancellationToken cancellationToken = default);



    /// <summary>
    /// 获取指定类型的系统级单例状态数据，使用类型名作为键
    /// </summary>
    /// <typeparam name="T">状态数据类型</typeparam>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回状态数据，获取失败返回null</returns>
    Task<T?> GetSingleStateAsync<T>(CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 保存状态数据，使用类型名作为键前缀
    /// </summary>
    /// <param name="key">状态键</param>
    /// <param name="value">状态值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="ttl">生存时间，填入0代表永久存储，不受全局设置影响</param>
    /// <returns>保存失败抛出异常</returns>
    Task SaveStateAsync<T>(string key, T value,
        CancellationToken cancellationToken = default, TimeSpan? ttl = null);

    /// <summary>
    /// 保存状态数据，使用指定的键前缀
    /// </summary>
    /// <param name="key">状态键</param>
    /// <param name="value">状态值</param>
    /// <param name="prefix">键前缀</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="ttl">生存时间，填入0代表永久存储，不受全局设置影响</param>
    /// <returns>保存失败抛出异常</returns>
    Task SaveStateAsync<T>(string key, T value, string? prefix, CancellationToken cancellationToken = default, TimeSpan? ttl = null);

    /// <summary>
    /// 保存指定类型的系统级单例状态数据，使用类型名作为键
    /// </summary>
    /// <typeparam name="T">状态数据类型</typeparam>
    /// <param name="value">状态值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="ttl">生存时间，填入0代表永久存储，不受全局设置影响</param>
    /// <returns>保存失败抛出异常</returns>
    Task SaveSingleStateAsync<T>(T value, CancellationToken cancellationToken = default, TimeSpan? ttl = null)
        where T : class;

    /// <summary>
    /// 删除指定键的状态数据
    /// </summary>
    /// <param name="key">状态键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除操作的任务</returns>
    Task DeleteStateAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定键的状态数据，使用指定的键前缀
    /// </summary>
    /// <param name="key">状态键</param>
    /// <param name="prefix">键前缀</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除操作的任务</returns>
    Task DeleteStateAsync(string key, string? prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定类型的系统级单例状态数据，使用类型名作为键
    /// </summary>
    /// <typeparam name="T">状态数据类型</typeparam>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除操作的任务</returns>
    Task DeleteSingleStateAsync<T>(CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 批量删除状态数据，不使用键前缀
    /// </summary>
    /// <param name="keys">状态键列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除操作的任务</returns>
    Task DeleteBulkStateAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除状态数据，使用指定的键前缀
    /// </summary>
    /// <param name="keys">状态键列表</param>
    /// <param name="prefix">键前缀</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除操作的任务</returns>
    Task DeleteBulkStateAsync(IReadOnlyList<string> keys, string? prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取状态数据及其版本标识，使用泛型T的类型名作为键前缀
    /// </summary>
    /// <typeparam name="T">状态数据类型</typeparam>
    /// <param name="key">状态键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回包含状态值和版本标识的元组</returns>
    Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取状态数据及其版本标识，使用指定的键前缀
    /// </summary>
    /// <typeparam name="T">状态数据类型</typeparam>
    /// <param name="key">状态键</param>
    /// <param name="prefix">键前缀</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回包含状态值和版本标识的元组</returns>
    Task<(T value, string etag)> GetStateAndVersionAsync<T>(string key, string? prefix,
        CancellationToken cancellationToken = default);
}
