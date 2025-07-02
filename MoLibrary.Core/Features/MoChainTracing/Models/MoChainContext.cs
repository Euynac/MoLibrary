using System.Collections.Concurrent;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoLibrary.Core.Features.MoChainTracing.Models;

/// <summary>
/// 调用链上下文，用于存储整个调用链的信息
/// </summary>
public class MoChainContext
{
    /// <summary>
    /// 调用链的根节点
    /// </summary>
    public MoChainNode? Root { get; set; }

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

    [JsonIgnore]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 调用链结束时间
    /// </summary>
    [JsonIgnore]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 总执行时间（毫秒）
    /// </summary>
    public string? TotalDuration => EndTime?.Subtract(StartTime).TotalMilliseconds is { } milliseconds ? $"{milliseconds}ms" : null;

    /// <summary>
    /// 其他信息
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandoObject? OtherInfo { get; set; }

    /// <summary>
    /// 添加一个新的调用链节点
    /// </summary>
    /// <param name="node">调用链节点</param>
    public void AddNode(MoChainNode node)
    {
        if (Root == null)
        {
            Root = node;
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
    /// <param name="exception">异常信息</param>
    /// <param name="extraInfo">额外信息</param>
    public void CompleteNode(string traceId, string? result = null, bool success = true, Exception? exception = null, object? extraInfo = null)
    {
        if (NodeMap.TryGetValue(traceId, out var node))
        {
            node.EndTime = DateTime.UtcNow;
            node.Result = result;
            node.Exception = exception;
            node.EndExtraInfo = extraInfo;

            // 如果有异常，自动设置为失败
            if (exception != null || !success)
            {
                node.IsFailed = true;
            }

            // 从活跃节点栈中移除
            if (ActiveNodes.Count > 0 && ActiveNodes.Peek().TraceId == traceId)
            {
                ActiveNodes.Pop();
            }
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

    /// <summary>
    /// 合并远程调用链信息
    /// </summary>
    /// <param name="traceId">当前调用链节点标识</param>
    /// <param name="remoteChainInfo">远程调用链信息</param>
    public void MergeRemoteChain(string traceId, object? remoteChainInfo)
    {
        if (remoteChainInfo == null || !NodeMap.TryGetValue(traceId, out var currentNode))
        {
            return;
        }

        try
        {
            // 尝试从不同的数据源解析远程调用链信息
            MoChainNode? remoteRootNode = null;

            if (remoteChainInfo is JsonElement jsonElement)
            {
                remoteRootNode = ExtractChainFromJsonElement(jsonElement);
            }
            else if (remoteChainInfo is string jsonString)
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(jsonString);
                    remoteRootNode = ExtractChainFromJsonElement(jsonDoc.RootElement);
                }
                catch (JsonException)
                {
                    // 忽略 JSON 解析错误
                }
            }
            

            if (remoteRootNode != null)
            {
                // 标记为远程调用
                MarkAsRemoteCall(remoteRootNode);
                
                // 将远程调用链作为当前节点的子节点
                currentNode.Children ??= [];
                currentNode.Children.Add(remoteRootNode);
                remoteRootNode.Parent = currentNode;

                // 更新节点映射
                AddNodeToMap(remoteRootNode);
            }
        }
        catch (Exception)
        {
            // 忽略合并过程中的异常，不影响主流程
        }
    }

    /// <summary>
    /// 从 JsonElement 中提取调用链信息
    /// </summary>
    /// <param name="jsonElement">JSON 元素</param>
    /// <returns>调用链根节点</returns>
    private static MoChainNode? ExtractChainFromJsonElement(JsonElement jsonElement)
    {
        try
        {
            // 检查是否有 chainTracing 字段
            if (jsonElement.TryGetProperty("chainTracing", out var chainTracingElement))
            {
                if (chainTracingElement.TryGetProperty("rootNode", out var rootNodeElement))
                {
                    return JsonSerializer.Deserialize<MoChainNode>(rootNodeElement.GetRawText());
                }
            }

            // 检查是否直接是 rootNode
            if (jsonElement.TryGetProperty("traceId", out _) && 
                jsonElement.TryGetProperty("handler", out _))
            {
                return JsonSerializer.Deserialize<MoChainNode>(jsonElement.GetRawText());
            }
        }
        catch (JsonException)
        {
            // 忽略 JSON 解析错误
        }

        return null;
    }

  

    /// <summary>
    /// 标记节点及其子节点为远程调用
    /// </summary>
    /// <param name="node">节点</param>
    private static void MarkAsRemoteCall(MoChainNode node)
    {
        node.IsRemoteCall = true;
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                MarkAsRemoteCall(child);
            }
        }
    }

    /// <summary>
    /// 将节点及其子节点添加到映射中
    /// </summary>
    /// <param name="node">节点</param>
    private void AddNodeToMap(MoChainNode node)
    {
        NodeMap[node.TraceId] = node;
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                AddNodeToMap(child);
            }
        }
    }
}