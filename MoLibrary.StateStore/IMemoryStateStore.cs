namespace MoLibrary.StateStore;

/// <summary>
/// 内存状态存储接口
/// </summary>
public interface IMemoryStateStore : IMoStateStore
{
    /// <summary>
    /// 获取状态和 ETag，返回原始 object 类型值（用于不知道具体类型的场景，如 UI 展示）
    /// </summary>
    /// <param name="key">状态键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>值（object 类型）和 ETag 的元组</returns>
    Task<(object? Value, string ETag)> GetStateAndETagRawAsync(string key, CancellationToken cancellationToken = default);
}