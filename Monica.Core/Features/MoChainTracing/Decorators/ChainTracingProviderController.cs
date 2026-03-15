using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Tool.Extensions;
using Monica.Tool.MoResponse;

namespace Monica.Core.Features.MoChainTracing.Decorators;

/// <summary>
/// Action filter that wraps controller execution in chain tracing.
/// </summary>
/// <param name="chainTracing">The chain tracing service.</param>
/// <param name="logger">The logger.</param>
public class ChainTracingProviderController(IMoChainTracing chainTracing, ILogger<ChainTracingProviderController> logger) : IActionFilter
{

    /// <summary>
    /// Runs before the action executes.
    /// </summary>
    /// <param name="context">The action execution context.</param>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var controllerName = context.Controller.GetType().Name;
        var actionName = context.ActionDescriptor.DisplayName ?? context.ActionDescriptor.RouteValues["action"] ?? "Unknown";
        
        var actionTraceId = chainTracing.BeginTrace(actionName, $"Controller({controllerName})");

        // Store the trace id so the completion step can finish the same node.
        context.HttpContext.Items[nameof(ChainTracingProviderController)] = actionTraceId;
    }

    /// <summary>
    /// Runs after the action executes.
    /// </summary>
    /// <param name="context">The action execution context.</param>
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
                // Complete the trace with exception details.
                chainTracing.EndTrace(actionTraceId, $"Exception: {context.Exception.GetMessageRecursively()}", false, context.Exception);
            }
            else
            {
                // Derive a concise result description when the action returned IMoResponse.
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
