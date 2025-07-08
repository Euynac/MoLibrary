using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Extensions;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Features.MoChainTracing.Decorators;

/// <summary>
/// 调用链追踪 Action Filter
/// </summary>
/// <param name="chainTracing">调用链追踪服务</param>
/// <param name="logger">日志记录器</param>
public class ChainTracingProviderController(IMoChainTracing chainTracing, ILogger<ChainTracingProviderController> logger) : IActionFilter
{

    /// <summary>
    /// Action 执行前
    /// </summary>
    /// <param name="context">Action 执行上下文</param>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var controllerName = context.Controller.GetType().Name;
        var actionName = context.ActionDescriptor.DisplayName ?? context.ActionDescriptor.RouteValues["action"] ?? "Unknown";
        
        // 记录控制器 Action 的开始
        var actionTraceId = chainTracing.BeginTrace(actionName, $"Controller({controllerName})");

        // 将 TraceId 存储到 ActionContext 中，以便在 OnActionExecuted 中使用
        context.HttpContext.Items[nameof(ChainTracingProviderController)] = actionTraceId;
    }

    /// <summary>
    /// Action 执行后
    /// </summary>
    /// <param name="context">Action 执行上下文</param>
    public void OnActionExecuted(ActionExecutedContext context)
    {
        var actionTraceId = context.HttpContext.Items[nameof(ChainTracingProviderController)]?.ToString();
        if (string.IsNullOrEmpty(actionTraceId))
        {
            return;
        }


        try
        {
            if (context.Exception != null)
            {
                // 记录异常
                chainTracing.EndTrace(actionTraceId, $"Exception: {context.Exception.GetMessageRecursively()}", false, context.Exception);
            }
            else
            {
                // 检查返回结果
                var result = ChainTracingHelper.ExtractResult(context.Result);
                if (result is IMoResponse response)
                {
                    chainTracing.EndTrace(actionTraceId, $"{ChainTracingHelper.GetResponseTypeName(response.GetType())}({response.Code}){(response.Message?.LimitMaxLength(100, "...").BeNullIfWhiteSpace() is { } msg ? $"[{msg}]" : null)}", response.Code == ResponseCode.Ok);
                  
                }
                else
                {
                    chainTracing.EndTrace(actionTraceId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "处理 Controller Action 调用链时发生异常");
        }
    }
}