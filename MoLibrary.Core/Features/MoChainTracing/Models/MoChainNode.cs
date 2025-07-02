using System.Text.Json.Serialization;

namespace MoLibrary.Core.Features.MoChainTracing.Models;

/// <summary>
/// 调用链节点，表示一个具体的调用
/// </summary>
public class MoChainNode
{
    /// <summary>
    /// 调用链节点类型
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public EChainTracingType Type { get; set; } = EChainTracingType.Unknown;

    /// <summary>
    /// 调用链节点唯一标识
    /// </summary>
    [JsonIgnore]
    public string TraceId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 处理者名称（如服务名、类名等）
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Handler { get; set; }

    /// <summary>
    /// 操作名称（如方法名、操作描述等）
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// 开始时间
    /// </summary>
    [JsonIgnore]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 结束时间
    /// </summary>
    [JsonIgnore]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public string? Duration =>
        EndTime?.Subtract(StartTime).TotalMilliseconds is { } milliseconds ? $"{milliseconds}ms" : null;

    /// <summary>
    /// 调用结果描述
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Result { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsFailed { get; set; }

    /// <summary>
    /// 是否为远程调用
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsRemoteCall { get; set; }

    /// <summary>
    /// 异常信息
    /// </summary>
    [JsonIgnore]
    public Exception? Exception { get; set; }

    /// <summary>
    /// 异常信息的序列化表示
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? ExceptionMessage => Exception?.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// 开始时的额外信息
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? StartExtraInfo { get; set; }

    /// <summary>
    /// 结束时的额外信息
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? EndExtraInfo { get; set; }

    /// <summary>
    /// 子调用链节点
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<MoChainNode>? Children { get; set; }

    /// <summary>
    /// 父调用链节点
    /// </summary>
    [JsonIgnore]
    public MoChainNode? Parent { get; set; }

    /// <summary>
    /// 备注信息
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Remarks { get; set; }
}