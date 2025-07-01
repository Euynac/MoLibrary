using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Modules;

namespace MoLibrary.Core.Features.MoChainTracing;

/// <summary>
/// 基于 AsyncLocal 的调用链追踪实现
/// </summary>
/// <remarks>
/// 构造函数
/// </remarks>
/// <param name="options">调用链追踪配置选项</param>
/// <param name="logger">日志记录器</param>
public class AsyncLocalMoChainTracing(IOptions<ModuleChainTracingOption> options, ILogger<AsyncLocalMoChainTracing>? logger = null) : IMoChainTracing
{
    private static readonly AsyncLocal<MoChainContext?> _chainContext = new();
    private readonly ModuleChainTracingOption _options = options.Value;

    /// <summary>
    /// 开始一个新的调用链节点
    /// </summary>
    /// <param name="handler">处理者名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>调用链节点标识</returns>
    public string BeginTrace(string handler, string operation, object? extraInfo = null)
    {
        try
        {
            var context = _chainContext.Value ??= new MoChainContext();

            // 检查最大调用链深度
            if (IsMaxDepthReached())
            {
                logger?.LogWarning("调用链深度已达到最大限制 {MaxChainDepth}，跳过创建新节点: {Handler}.{Operation}", 
                    _options.MaxChainDepth, handler, operation);
                return Guid.NewGuid().ToString("N"); // 返回虚拟 TraceId，避免后续调用报错
            }

            // 检查最大节点数量
            if (IsMaxNodeCountReached())
            {
                logger?.LogWarning("调用链节点数量已达到最大限制 {MaxNodeCount}，跳过创建新节点: {Handler}.{Operation}", 
                    _options.MaxNodeCount, handler, operation);
                return Guid.NewGuid().ToString("N"); // 返回虚拟 TraceId，避免后续调用报错
            }

            var node = new MoChainNode
            {
                Handler = handler,
                Operation = operation,
                StartExtraInfo = extraInfo,
                StartTime = DateTime.UtcNow
            };

            context.AddNode(node);

            logger?.LogDebug("开始调用链节点: {Handler}.{Operation}, TraceId: {TraceId}, 当前深度: {Depth}, 总节点数: {NodeCount}", 
                handler, operation, node.TraceId, context.ActiveNodes.Count, context.NodeMap.Count);

            return node.TraceId;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "开始调用链节点时发生异常: {Handler}.{Operation}", handler, operation);
            return Guid.NewGuid().ToString("N"); // 返回一个虚拟的 TraceId，避免后续调用报错
        }
    }

    /// <summary>
    /// 完成一个调用链节点
    /// </summary>
    /// <param name="traceId">调用链节点标识</param>
    /// <param name="result">调用结果描述</param>
    /// <param name="success">是否成功</param>
    /// <param name="exception">异常信息</param>
    /// <param name="extraInfo">额外信息</param>
    public void EndTrace(string traceId, string? result = null, bool success = true, Exception? exception = null, object? extraInfo = null)
    {
        try
        {
            var context = _chainContext.Value;
            if (context == null)
            {
                logger?.LogWarning("尝试完成调用链节点但当前上下文为空: TraceId: {TraceId}", traceId);
                return;
            }

            context.CompleteNode(traceId, result, success, exception, extraInfo);

            logger?.LogDebug("完成调用链节点: TraceId: {TraceId}, Success: {Success}, Result: {Result}", 
                traceId, success, result);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "完成调用链节点时发生异常: TraceId: {TraceId}", traceId);
        }
    }

    /// <summary>
    /// 记录简单的调用信息
    /// </summary>
    /// <param name="handler">处理者名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="result">调用结果</param>
    /// <param name="duration">执行时间</param>
    /// <param name="extraInfo">额外信息</param>
    public void RecordTrace(string handler, string operation, bool success = true, string? result = null, 
        TimeSpan? duration = null, object? extraInfo = null)
    {
        try
        {
            var context = _chainContext.Value ??= new MoChainContext();

            // 检查最大节点数量
            if (IsMaxNodeCountReached())
            {
                logger?.LogWarning("调用链节点数量已达到最大限制 {MaxNodeCount}，跳过记录简单调用: {Handler}.{Operation}", 
                    _options.MaxNodeCount, handler, operation);
                return;
            }

            var node = new MoChainNode
            {
                Handler = handler,
                Operation = operation,
                StartTime = DateTime.UtcNow,
                Success = success,
                Result = result,
                StartExtraInfo = extraInfo,
                EndExtraInfo = extraInfo
            };

            if (duration.HasValue)
            {
                node.EndTime = node.StartTime.Add(duration.Value);
            }
            else
            {
                node.EndTime = DateTime.UtcNow;
            }

            context.AddNode(node);
            context.CompleteNode(node.TraceId, result, success, null, extraInfo);

            logger?.LogDebug("记录简单调用链: {Handler}.{Operation}, Success: {Success}, Duration: {Duration}ms, 总节点数: {NodeCount}", 
                handler, operation, success, node.DurationMs, context.NodeMap.Count);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "记录简单调用链时发生异常: {Handler}.{Operation}", handler, operation);
        }
    }

    /// <summary>
    /// 获取当前的调用链信息
    /// </summary>
    /// <returns>调用链信息</returns>
    public MoChainContext? GetCurrentChain()
    {
        return _chainContext.Value;
    }

    /// <summary>
    /// 开始一个根调用链
    /// </summary>
    /// <param name="handler">处理者名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>调用链节点标识</returns>
    public static string BeginRootTrace(string handler, string operation, object? extraInfo = null)
    {
        var context = new MoChainContext();
        _chainContext.Value = context;

        var node = new MoChainNode
        {
            Handler = handler,
            Operation = operation,
            StartExtraInfo = extraInfo,
            StartTime = DateTime.UtcNow
        };

        context.AddNode(node);
        return node.TraceId;
    }

    /// <summary>
    /// 检查当前是否有活跃的调用链
    /// </summary>
    /// <returns>是否有活跃的调用链</returns>
    public bool HasActiveChain()
    {
        return _chainContext.Value != null;
    }

    /// <summary>
    /// 获取当前调用链的深度
    /// </summary>
    /// <returns>调用链深度</returns>
    public int GetChainDepth()
    {
        var context = _chainContext.Value;
        return context?.ActiveNodes.Count ?? 0;
    }

    /// <summary>
    /// 获取当前调用链的节点总数
    /// </summary>
    /// <returns>节点总数</returns>
    public int GetNodeCount()
    {
        var context = _chainContext.Value;
        return context?.NodeMap.Count ?? 0;
    }

    /// <summary>
    /// 检查是否达到最大深度限制
    /// </summary>
    /// <returns>是否达到限制</returns>
    public bool IsMaxDepthReached()
    {
        return GetChainDepth() >= _options.MaxChainDepth;
    }

    /// <summary>
    /// 检查是否达到最大节点数量限制
    /// </summary>
    /// <returns>是否达到限制</returns>
    public bool IsMaxNodeCountReached()
    {
        return GetNodeCount() >= _options.MaxNodeCount;
    }

    /// <summary>
    /// 合并远程调用链信息
    /// </summary>
    /// <param name="traceId">当前调用链节点标识</param>
    /// <param name="remoteChainInfo">远程调用链信息</param>
    public void MergeRemoteChain(string traceId, object? remoteChainInfo)
    {
        try
        {
            var context = _chainContext.Value;
            if (context == null)
            {
                logger?.LogWarning("尝试合并远程调用链但当前上下文为空: TraceId: {TraceId}", traceId);
                return;
            }

            context.MergeRemoteChain(traceId, remoteChainInfo);

            logger?.LogDebug("合并远程调用链: TraceId: {TraceId}", traceId);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "合并远程调用链时发生异常: TraceId: {TraceId}", traceId);
        }
    }
}