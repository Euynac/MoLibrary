namespace Monica.AI.Models;

/// <summary>
/// AI 模型元数据信息基类
/// </summary>
public abstract class AIModelInfo
{
    /// <summary>
    /// 模型名称
    /// </summary>
    public required string ModelName { get; init; }

    /// <summary>
    /// 模型描述
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// 大语言模型元数据信息
/// </summary>
public class LLMModelInfo : AIModelInfo
{
    /// <summary>
    /// 是否支持图像输入
    /// </summary>
    public bool SupportsImage { get; init; }

    /// <summary>
    /// 是否支持深度思考/推理
    /// </summary>
    public bool SupportsReasoning { get; init; }

    /// <summary>
    /// 上下文窗口大小（可选）
    /// </summary>
    public int? ContextWindow { get; init; }

    /// <summary>
    /// 最大输出 Token 数量（可选）
    /// </summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>
    /// 输入费用（USD / 1M tokens）
    /// </summary>
    public decimal InputCostPerMillionTokens { get; init; }

    /// <summary>
    /// 输出费用（USD / 1M tokens）
    /// </summary>
    public decimal OutputCostPerMillionTokens { get; init; }

    /// <summary>
    /// 缓存命中输入费用（USD / 1M tokens）
    /// </summary>
    public decimal CachedInputCostPerMillionTokens { get; init; }
}

/// <summary>
/// 图像模型元数据信息
/// </summary>
public class ImageModelInfo : AIModelInfo
{
    /// <summary>
    /// 最大图像尺寸（可选，例如 1024 表示 1024x1024）
    /// </summary>
    public int? MaxImageSize { get; init; }

    /// <summary>
    /// 是否支持编辑/变换
    /// </summary>
    public bool SupportsEditing { get; init; }
}

/// <summary>
/// 向量嵌入模型元数据信息
/// </summary>
public class EmbeddingModelInfo : AIModelInfo
{
    /// <summary>
    /// 向量维度
    /// </summary>
    public int? Dimensions { get; init; }
}

/// <summary>
/// 文本转语音模型元数据信息
/// </summary>
public class TextToSpeechModelInfo : AIModelInfo
{
    /// <summary>
    /// 可用音色列表
    /// </summary>
    public IReadOnlyList<string>? Voices { get; init; }

    /// <summary>
    /// 是否支持流式输出
    /// </summary>
    public bool SupportsStreaming { get; init; }
}
