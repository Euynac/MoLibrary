using Microsoft.Extensions.AI;
using MoLibrary.AI.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.AI.Abstractions;

/// <summary>
/// AI Provider 抽象接口，定义了 AI 服务提供者的基本操作
/// </summary>
public interface IAIProvider : IDisposable
{
    /// <summary>
    /// Provider 唯一标识符
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Provider 显示名称
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Provider 元数据信息
    /// </summary>
    AIProviderInfo Info { get; }

    /// <summary>
    /// 获取 IChatClient 实例
    /// </summary>
    /// <returns>IChatClient 实例</returns>
    IChatClient GetChatClient(string? modelName = null);

    /// <summary>
    /// 测试连接是否正常
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>连接测试结果</returns>
    Task<Res> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取可用的模型列表
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>可用模型列表</returns>
    Task<Res<IReadOnlyList<string>>> GetAvailableModelsAsync(CancellationToken ct = default);
}
