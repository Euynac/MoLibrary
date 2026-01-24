using System;

namespace MoLibrary.AI.Providers;

/// <summary>
/// AI Provider 配置基类
/// </summary>
public abstract class AIProviderOptions
{
    /// <summary>
    /// Provider 唯一标识符，如果不设置则自动生成
    /// </summary>
    public string? ProviderId { get; set; }

    /// <summary>
    /// Provider 显示名称
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// API 密钥
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// 默认使用的模型
    /// </summary>
    public string? DefaultModel { get; set; }

    /// <summary>
    /// 支持的模型列表（为空则使用预留模型）
    /// </summary>
    public IList<string>? SupportedModels { get; set; }

    /// <summary>
    /// API 基础 URL（可选，用于自定义端点）
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// 是否设为默认 Provider
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// OpenAI Provider 配置
/// </summary>
public class OpenAIProviderOptions : AIProviderOptions
{
    /// <summary>
    /// 组织 ID（可选）
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// 项目 ID（可选）
    /// </summary>
    public string? Project { get; set; }
}

/// <summary>
/// Anthropic Provider 配置
/// </summary>
public class AnthropicProviderOptions : AIProviderOptions
{
    /// <summary>
    /// 默认最大 Token 数量
    /// </summary>
    public int MaxTokens { get; set; } = 4096;
}
