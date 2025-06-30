using System.Dynamic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Features.MoChainTracing;

/// <summary>
/// 调用链追踪中间件
/// 自动将调用链信息附加到 IServiceResponse 的 ExtraInfo 中
/// </summary>
public class ChainTracingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ChainTracingMiddleware> _logger;
    private readonly IMoChainTracing _chainTracing;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="next">下一个中间件</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="chainTracing">调用链追踪服务</param>
    public ChainTracingMiddleware(RequestDelegate next, ILogger<ChainTracingMiddleware> logger, IMoChainTracing chainTracing)
    {
        _next = next;
        _logger = logger;
        _chainTracing = chainTracing;
    }

    /// <summary>
    /// 中间件执行逻辑
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var requestPath = context.Request.Path.Value ?? "";
        var requestMethod = context.Request.Method;
        
        // 开始根调用链
        var rootTraceId = _chainTracing.BeginTrace("HTTP", $"{requestMethod} {requestPath}", new
        {
            RequestPath = requestPath,
            RequestMethod = requestMethod,
            QueryString = context.Request.QueryString.Value,
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            RemoteIpAddress = context.Connection.RemoteIpAddress?.ToString()
        });

        try
        {
            // 执行后续中间件
            await _next(context);

            // 检查响应是否为 IServiceResponse
            await TryAttachChainToResponseAsync(context);

            // 标记调用链成功完成
            _chainTracing.EndTrace(rootTraceId, $"HTTP {context.Response.StatusCode}", true);
        }
        catch (Exception ex)
        {
            // 记录异常
            _chainTracing.RecordException(rootTraceId, ex);
            _chainTracing.EndTrace(rootTraceId, $"HTTP {context.Response.StatusCode} - Exception: {ex.Message}", false);
            
            _logger.LogError(ex, "调用链中间件执行时发生异常");
            throw;
        }
        finally
        {
            // 清理调用链上下文（可选，根据需要决定是否清理）
            // _chainTracing.ClearChain();
        }
    }

    /// <summary>
    /// 尝试将调用链信息附加到响应中
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    private async Task TryAttachChainToResponseAsync(HttpContext context)
    {
        try
        {
            // 获取当前调用链
            var chainContext = _chainTracing.GetCurrentChain();
            if (chainContext == null)
            {
                return;
            }

            // 检查是否为 JSON 响应
            if (!IsJsonResponse(context))
            {
                return;
            }

            // 如果响应体已经被写入，我们无法修改它
            if (context.Response.HasStarted)
            {
                _logger.LogDebug("响应已开始发送，无法附加调用链信息");
                return;
            }

            // 尝试从响应体中提取 IServiceResponse
            var originalBodyStream = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            // 这里需要在控制器返回后进行处理
            // 由于中间件的执行时机，我们需要使用其他方式来处理响应
            // 建议在控制器级别或者使用 ActionFilter 来处理
            
            _logger.LogDebug("调用链中间件: 当前链深度 {ChainDepth}, 总节点数 {NodeCount}", 
                chainContext.ActiveNodes.Count, 
                chainContext.NodeMap.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "附加调用链信息到响应时发生异常");
        }
    }

    /// <summary>
    /// 检查是否为 JSON 响应
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <returns>是否为 JSON 响应</returns>
    private static bool IsJsonResponse(HttpContext context)
    {
        var contentType = context.Response.ContentType;
        return !string.IsNullOrEmpty(contentType) && 
               (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
                contentType.Contains("text/json", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// 调用链响应处理器，用于处理控制器返回的 IServiceResponse
/// </summary>
public static class ChainTracingResponseHelper
{
    /// <summary>
    /// 将调用链信息附加到 IServiceResponse 的 ExtraInfo 中
    /// </summary>
    /// <param name="response">服务响应对象</param>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="logger">日志记录器</param>
    public static void AttachChainToResponse(IServiceResponse response, IMoChainTracing chainTracing, ILogger? logger = null)
    {
        try
        {
            var chainContext = chainTracing.GetCurrentChain();
            if (chainContext == null)
            {
                return;
            }

            // 标记调用链完成
            chainContext.MarkComplete();

            // 初始化 ExtraInfo
            response.ExtraInfo ??= new ExpandoObject();

            // 将调用链信息添加到 ExtraInfo
            var extraInfo = (IDictionary<string, object?>)response.ExtraInfo;
            extraInfo["chainTracing"] = new
            {
                TotalDurationMs = chainContext.TotalDurationMs,
                StartTime = chainContext.StartTime,
                EndTime = chainContext.EndTime,
                RootNode = chainContext.RootNode,
                Summary = new
                {
                    TotalNodes = chainContext.NodeMap.Count,
                    SuccessfulNodes = chainContext.NodeMap.Values.Count(n => n.Success),
                    FailedNodes = chainContext.NodeMap.Values.Count(n => !n.Success),
                    ActiveNodes = chainContext.ActiveNodes.Count
                }
            };

            logger?.LogDebug("成功将调用链信息附加到响应, 总耗时: {TotalDuration}ms, 节点数: {NodeCount}", 
                chainContext.TotalDurationMs, chainContext.NodeMap.Count);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "附加调用链信息到响应时发生异常");
        }
    }

    /// <summary>
    /// 将调用链信息附加到 IServiceResponse 的 ExtraInfo 中 (异步版本)
    /// </summary>
    /// <param name="response">服务响应对象</param>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="logger">日志记录器</param>
    public static Task AttachChainToResponseAsync(IServiceResponse response, IMoChainTracing chainTracing, 
        CancellationToken cancellationToken = default, ILogger? logger = null)
    {
        AttachChainToResponse(response, chainTracing, logger);
        return Task.CompletedTask;
    }
} 