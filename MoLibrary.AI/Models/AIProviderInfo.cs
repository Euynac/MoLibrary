namespace MoLibrary.AI.Models;

/// <summary>
/// AI Provider 元数据信息
/// </summary>
public class AIProviderInfo
{
    /// <summary>
    /// Provider 唯一标识符
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// Provider 显示名称
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Provider 描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Provider 类型（如 OpenAI, Anthropic 等）
    /// </summary>
    public required string ProviderType { get; init; }

    /// <summary>
    /// 默认使用的模型
    /// </summary>
    public string? DefaultModel { get; init; }

    /// <summary>
    /// 支持的模型列表
    /// </summary>
    public IReadOnlyList<string>? SupportedModels { get; init; }

    /// <summary>
    /// 是否支持流式响应
    /// </summary>
    public bool SupportsStreaming { get; init; } = true;

    /// <summary>
    /// 是否支持函数调用
    /// </summary>
    public bool SupportsFunctionCalling { get; init; }

    /// <summary>
    /// 是否是默认 Provider
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// 图标（用于 UI 显示）
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Provider 状态
    /// </summary>
    public AIProviderStatus Status { get; set; } = AIProviderStatus.Unknown;
}

/// <summary>
/// Provider 状态枚举
/// </summary>
public enum AIProviderStatus
{
    /// <summary>
    /// 未知状态
    /// </summary>
    Unknown,

    /// <summary>
    /// 可用
    /// </summary>
    Available,

    /// <summary>
    /// 不可用
    /// </summary>
    Unavailable,

    /// <summary>
    /// 配置错误
    /// </summary>
    ConfigurationError
}
