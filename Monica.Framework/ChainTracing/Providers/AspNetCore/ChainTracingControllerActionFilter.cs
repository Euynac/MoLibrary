using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.Core.Results.Abstractions;
using Monica.Framework.ChainTracing.Abstractions;
using Monica.Framework.ChainTracing.Services.Support;
using Monica.Tool.Extensions;

namespace Monica.Framework.ChainTracing.Providers.AspNetCore;

/// <summary>
/// Action filter that wraps controller execution in chain tracing.
/// </summary>
/// <param name="chainTracing">The chain tracing service.</param>
/// <param name="logger">The logger.</param>
public class ChainTracingControllerActionFilter(IChainTracing chainTracing, ILogger<ChainTracingControllerActionFilter> logger) : IActionFilter
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
        context.HttpContext.Items[nameof(ChainTracingControllerActionFilter)] = actionTraceId;
    }

    /// <summary>
    /// Runs after the action executes.
    /// </summary>
    /// <param name="context">The action execution context.</param>
    public void OnActionExecuted(ActionExecutedContext context)
    {
        var actionTraceId = context.HttpContext.Items[nameof(ChainTracingControllerActionFilter)]?.ToString();
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
                // Derive a concise result description when the action returned an IResultEnvelope.
                var result = ChainTracingResultHelper.ExtractResult(context.Result);
                if (result is IResultEnvelope response)
                {
                    chainTracing.EndTrace(actionTraceId, $"{ChainTracingResultHelper.GetResponseTypeName(response.GetType())}({response.Status}){(response.Message?.LimitMaxLength(100, "...").BeNullIfWhiteSpace() is { } msg ? $"[{msg}]" : null)}", response.Status == ResStatus.Ok);
                  
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
