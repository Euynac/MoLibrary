using System.Collections.Concurrent;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoLibrary.Core.Features.MoChainTracing;

/// <summary>
/// 调用链上下文，用于存储整个调用链的信息
/// </summary>
public class MoChainContext
{
    /// <summary>
    /// 调用链的根节点
    /// </summary>
    public MoChainNode? RootNode { get; set; }

    /// <summary>
    /// 当前活跃的调用链节点
    /// </summary>
    [JsonIgnore]
    public Stack<MoChainNode> ActiveNodes { get; set; } = new();

    /// <summary>
    /// 调用链节点映射（用于快速查找）
    /// </summary>
    [JsonIgnore]
    public ConcurrentDictionary<string, MoChainNode> NodeMap { get; set; } = new();

    /// <summary>
    /// 调用链开始时间
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 调用链结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 总执行时间（毫秒）
    /// </summary>
    public double? TotalDurationMs => EndTime?.Subtract(StartTime).TotalMilliseconds;

    /// <summary>
    /// 其他信息
    /// </summary>
    public ExpandoObject? OtherInfo { get; set; }

    /// <summary>
    /// 添加一个新的调用链节点
    /// </summary>
    /// <param name="node">调用链节点</param>
    public void AddNode(MoChainNode node)
    {
        if (RootNode == null)
        {
            RootNode = node;
        }
        else if (ActiveNodes.Count > 0)
        {
            var parent = ActiveNodes.Peek();
            parent.Children ??= [];
            parent.Children.Add(node);
            node.Parent = parent;
        }

        ActiveNodes.Push(node);
        NodeMap[node.TraceId] = node;
    }

    /// <summary>
    /// 完成一个调用链节点
    /// </summary>
    /// <param name="traceId">调用链节点标识</param>
    /// <param name="result">调用结果</param>
    /// <param name="success">是否成功</param>
    /// <param name="extraInfo">额外信息</param>
    public void CompleteNode(string traceId, string? result = null, bool success = true, object? extraInfo = null)
    {
        if (NodeMap.TryGetValue(traceId, out var node))
        {
            node.EndTime = DateTime.UtcNow;
            node.Result = result;
            node.Success = success;
            node.EndExtraInfo = extraInfo;

            // 从活跃节点栈中移除
            if (ActiveNodes.Count > 0 && ActiveNodes.Peek().TraceId == traceId)
            {
                ActiveNodes.Pop();
            }
        }
    }

    /// <summary>
    /// 记录异常信息
    /// </summary>
    /// <param name="traceId">调用链节点标识</param>
    /// <param name="exception">异常信息</param>
    /// <param name="extraInfo">额外信息</param>
    public void RecordException(string traceId, Exception exception, object? extraInfo = null)
    {
        if (NodeMap.TryGetValue(traceId, out var node))
        {
            node.Exception = exception;
            node.Success = false;
            node.EndExtraInfo = extraInfo;
        }
    }

    /// <summary>
    /// 标记调用链结束
    /// </summary>
    public void MarkComplete()
    {
        EndTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 获取调用链的深度克隆
    /// </summary>
    /// <returns>调用链的深度克隆</returns>
    public MoChainContext Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<MoChainContext>(json) ?? new MoChainContext();
    }
}

/// <summary>
/// 调用链节点，表示一个具体的调用
/// </summary>
public class MoChainNode
{
    /// <summary>
    /// 调用链节点唯一标识
    /// </summary>
    public string TraceId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 处理者名称（如服务名、类名等）
    /// </summary>
    public string Handler { get; set; } = string.Empty;

    /// <summary>
    /// 操作名称（如方法名、操作描述等）
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public double? DurationMs => EndTime?.Subtract(StartTime).TotalMilliseconds;

    /// <summary>
    /// 调用结果描述
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// 是否为远程调用
    /// </summary>
    public bool IsRemoteCall { get; set; }

    /// <summary>
    /// 异常信息
    /// </summary>
    [JsonIgnore]
    public Exception? Exception { get; set; }

    /// <summary>
    /// 异常信息的序列化表示
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExceptionMessage => Exception?.Message;

    /// <summary>
    /// 异常堆栈跟踪
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExceptionStackTrace => Exception?.StackTrace;

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