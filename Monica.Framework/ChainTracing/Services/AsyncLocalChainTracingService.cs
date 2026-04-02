using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.Results.Abstractions;
using Monica.Framework.ChainTracing.Abstractions;
using Monica.Framework.ChainTracing.Models;
using Monica.Modules;
using Monica.Tool.Extensions;
using Monica.Tool.General;

namespace Monica.Framework.ChainTracing.Services;

/// <summary>
/// Chain tracing implementation backed by <see cref="AsyncLocal{T}" />.
/// </summary>
/// <remarks>
/// Creates a new <see cref="AsyncLocalChainTracingService" /> instance.
/// </remarks>
/// <param name="options">Chain tracing configuration options.</param>
/// <param name="logger">The logger.</param>
/// <param name="jsonSerializerOptionsProvider">Global JSON serialization options.</param>
public class AsyncLocalChainTracingService(IOptions<ModuleChainTracingOption> options, ILogger<AsyncLocalChainTracingService> logger, IJsonSerializerOptionsProvider jsonSerializerOptionsProvider) : IChainTracing
{
    private static readonly AsyncLocal<ChainTraceContext?> _chainContext = new();
    private readonly ModuleChainTracingOption _options = options.Value;

    /// <summary>
    /// Starts a new trace node.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="handler">The handler name.</param>
    /// <param name="extraInfo">Optional extra metadata.</param>
    /// <param name="type">The traced operation type.</param>
    /// <returns>The trace identifier.</returns>
    public string BeginTrace(string operation, string? handler, object? extraInfo = null,
        EChainTracingType type = EChainTracingType.Unknown)
    {
        try
        {
            var context = _chainContext.Value ??= new ChainTraceContext();

            if (IsMaxDepthReached())
            {
                logger.LogWarning("调用链深度已达到最大限制 {MaxChainDepth}，跳过创建新节点: {Handler}.{Operation}", 
                    _options.MaxChainDepth, handler, operation);
                return Guid.NewGuid().ToString("N"); // Return a synthetic TraceId so follow-up calls stay safe.
            }

            if (IsMaxNodeCountReached())
            {
                logger.LogWarning("调用链节点数量已达到最大限制 {MaxNodeCount}，跳过创建新节点: {Handler}.{Operation}", 
                    _options.MaxNodeCount, handler, operation);
                return Guid.NewGuid().ToString("N"); // Return a synthetic TraceId so follow-up calls stay safe.
            }

            var node = new ChainTraceNode
            {
                Handler = handler,
                Operation = operation,
                Type = type,
                StartExtraInfo = extraInfo,
                StartTime = DateTime.UtcNow
            };

            context.AddNode(node);

            logger.LogDebug("开始调用链节点: {Handler}.{Operation}, TraceId: {TraceId}, 当前深度: {Depth}, 总节点数: {NodeCount}", 
                handler, operation, node.TraceId, context.ActiveNodes.Count, context.NodeMap.Count);

            return node.TraceId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "开始调用链节点时发生异常: {Handler}.{Operation}", handler, operation);
            return Guid.NewGuid().ToString("N"); // Return a synthetic TraceId so follow-up calls stay safe.
        }
    }

    /// <summary>
    /// Completes a trace node.
    /// </summary>
    /// <param name="traceId">The trace identifier.</param>
    /// <param name="result">A description of the result.</param>
    /// <param name="success">Whether the operation succeeded.</param>
    /// <param name="exception">The captured exception, if any.</param>
    /// <param name="extraInfo">Optional completion metadata.</param>
    public void EndTrace(string traceId, string? result = null, bool success = true, Exception? exception = null,
        object? extraInfo = null)
    {
        try
        {
            var context = _chainContext.Value;
            if (context == null)
            {
                logger.LogWarning("尝试完成调用链节点但当前上下文为空: TraceId: {TraceId}", traceId);
                return;
            }

            context.CompleteNode(traceId, result, success, exception, extraInfo);

            logger.LogDebug("完成调用链节点: TraceId: {TraceId}, Success: {Success}, Result: {Result}", 
                traceId, success, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "完成调用链节点时发生异常: TraceId: {TraceId}", traceId);
        }
    }

    /// <summary>
    /// Records a one-shot trace entry.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="handler">The handler name.</param>
    /// <param name="success">Whether the operation succeeded.</param>
    /// <param name="result">A description of the result.</param>
    /// <param name="duration">The known execution duration.</param>
    /// <param name="extraInfo">Optional extra metadata.</param>
    /// <param name="type">The traced operation type.</param>
    public void RecordTrace(string operation, string? handler, bool success = true, string? result = null,
        TimeSpan? duration = null, object? extraInfo = null, EChainTracingType type = EChainTracingType.Unknown)
    {
        try
        {
            var context = _chainContext.Value ??= new ChainTraceContext();

            if (IsMaxNodeCountReached())
            {
                logger.LogWarning("调用链节点数量已达到最大限制 {MaxNodeCount}，跳过记录简单调用: {Handler}.{Operation}", 
                    _options.MaxNodeCount, handler, operation);
                return;
            }

            var node = new ChainTraceNode
            {
                Handler = handler,
                Operation = operation,
                StartTime = DateTime.UtcNow,
                IsFailed = !success ? true : null,
                Result = result,
                Type = type,
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

            logger.LogDebug("记录简单调用链: {Handler}.{Operation}, Success: {Success}, Duration: {Duration}ms, 总节点数: {NodeCount}", 
                handler, operation, success, node.Duration, context.NodeMap.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "记录简单调用链时发生异常: {Handler}.{Operation}", handler, operation);
        }
    }

    /// <summary>
    /// Gets the current call-chain context.
    /// </summary>
    /// <returns>The current chain context.</returns>
    public ChainTraceContext? GetCurrentChain()
    {
        return _chainContext.Value;
    }

    /// <summary>
    /// Checks whether a chain is currently active.
    /// </summary>
    /// <returns><see langword="true" /> when a chain exists; otherwise, <see langword="false" />.</returns>
    public bool HasActiveChain()
    {
        return _chainContext.Value != null;
    }

    /// <summary>
    /// Gets the current chain depth.
    /// </summary>
    /// <returns>The number of active nodes in the chain.</returns>
    public int GetChainDepth()
    {
        var context = _chainContext.Value;
        return context?.ActiveNodes.Count ?? 0;
    }

    /// <summary>
    /// Gets the total number of tracked nodes in the current chain.
    /// </summary>
    /// <returns>The number of tracked nodes.</returns>
    public int GetNodeCount()
    {
        var context = _chainContext.Value;
        return context?.NodeMap.Count ?? 0;
    }

    /// <summary>
    /// Checks whether the configured depth limit has been reached.
    /// </summary>
    /// <returns><see langword="true" /> when the depth limit is reached.</returns>
    public bool IsMaxDepthReached()
    {
        return GetChainDepth() >= _options.MaxChainDepth;
    }

    /// <summary>
    /// Checks whether the configured node-count limit has been reached.
    /// </summary>
    /// <returns><see langword="true" /> when the node-count limit is reached.</returns>
    public bool IsMaxNodeCountReached()
    {
        return GetNodeCount() >= _options.MaxNodeCount;
    }

    /// <summary>
    /// Merges chain data returned from a remote call.
    /// </summary>
    /// <param name="traceId">The local trace identifier that should receive the remote chain.</param>
    /// <param name="remoteRes">The remote response carrying chain metadata.</param>
    public void MergeRemoteChain(string traceId, IResultEnvelope remoteRes)
    {
        try
        {
            var context = _chainContext.Value;
            if (context == null)
            {
                logger.LogWarning("尝试合并远程调用链但当前上下文为空: TraceId: {TraceId}", traceId);
                return;
            }
            var success = false;
          
            if (remoteRes.Metadata is { } expando)
            {
                if (expando.GetOrDefault(jsonSerializerOptionsProvider.UsingJsonDictionaryKeyPolicy(ChainTraceContext.CHAIN_KEY)) is JsonElement
                        jsonElement && jsonElement.Deserialize<ChainTraceNode>(jsonSerializerOptionsProvider.SerializerOptions) is {} chainNode)
                {
                    chainNode.EndExtraInfo = expando.Unfold().Where(p => p.Key != ChainTraceContext.CHAIN_KEY).ToDictionary();
                    success = context.MergeRemoteChain(traceId, chainNode, _options.MaxChainDepth);
                }
            }

            if (success) return;

            var remoteChainInfoStr = remoteRes.ToJsonString()?.LimitMaxLength(3000, "...");
            logger.LogWarning("合并远程调用链失败: TraceId: {TraceId}, RemoteChainInfo: {RemoteChainInfo}",
                traceId, remoteChainInfoStr);
        }
        catch (Exception ex)
        {
            var remoteChainInfoStr = remoteRes.ToJsonString()?.LimitMaxLength(3000, "...");
            logger.LogError(ex, "合并远程调用链时发生异常: TraceId: {TraceId}, RemoteChainInfo: {RemoteChainInfo}", 
                traceId, remoteChainInfoStr);
        }
    }

    public void Init()
    {
        _chainContext.Value ??= new ChainTraceContext();
    }

    public bool ContainsTrace(string traceId)
    {
        var context = _chainContext.Value;
        return context?.NodeMap.ContainsKey(traceId) ?? false;
    }

}
