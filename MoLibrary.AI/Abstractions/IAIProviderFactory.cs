using MoLibrary.AI.Models;

namespace MoLibrary.AI.Abstractions;

/// <summary>
/// AI Provider 工厂接口，用于创建和管理 Provider 实例
/// </summary>
public interface IAIProviderFactory
{
    /// <summary>
    /// 获取指定 ID 的 Provider
    /// </summary>
    /// <param name="providerId">Provider ID</param>
    /// <returns>Provider 实例，如果不存在则返回 null</returns>
    IAIProvider? GetProvider(string providerId);

    /// <summary>
    /// 获取所有已注册的 Provider
    /// </summary>
    /// <returns>Provider 列表</returns>
    IReadOnlyList<IAIProvider> GetAllProviders();

    /// <summary>
    /// 获取所有 Provider 的元数据信息
    /// </summary>
    /// <returns>Provider 信息列表</returns>
    IReadOnlyList<AIProviderInfo> GetAllProviderInfos();

    /// <summary>
    /// 获取默认 Provider
    /// </summary>
    /// <returns>默认 Provider 实例</returns>
    IAIProvider? GetDefaultProvider();

    /// <summary>
    /// 检查指定 Provider 是否已注册
    /// </summary>
    /// <param name="providerId">Provider ID</param>
    /// <returns>是否已注册</returns>
    bool HasProvider(string providerId);
}
