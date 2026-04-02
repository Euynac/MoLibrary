using System.Dynamic;
using Microsoft.AspNetCore.Mvc.Filters;
using Monica.Core.Results.Abstractions;
using Monica.Framework.ChainTracing.Abstractions;
using Monica.Framework.ChainTracing.Models;
using Monica.Framework.ChainTracing.Services.Support;
using Monica.Tool.Extensions;

namespace Monica.Framework.ChainTracing.Providers.AspNetCore;

/// <summary>
/// Attaches chain data to controller responses that implement <see cref="IResultEnvelope" />.
/// </summary>
public class ChainTracingResultMetadataActionFilter(IChainTracing chainTracing) : IActionFilter
{
    /// <summary>
    /// Runs before the action executes.
    /// </summary>
    /// <param name="context">The action execution context.</param>
    public void OnActionExecuting(ActionExecutingContext context) { }

    /// <summary>
    /// Runs after the action executes.
    /// </summary>
    /// <param name="context">The action execution context.</param>
    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (chainTracing.GetCurrentChain() is not { } chain ||
            ChainTracingResultHelper.ExtractResult(context.Result) is not IResultEnvelope serviceResponse)
        {
            return;
        }

        chain.MarkComplete();
        serviceResponse.Metadata ??= new ExpandoObject();
        serviceResponse.Metadata.Append(ChainTraceContext.CHAIN_KEY, chain.Root);

        if (chain.IsolatedNodes is not null)
        {
            serviceResponse.Metadata.Append($"{ChainTraceContext.CHAIN_KEY}_error", chain.IsolatedNodes.Select(p => new
            {
                p.Operation,
                p.Handler,
                p.Duration,
                p.Type,
                p.ExceptionMessage,
                p.StartTime,
                p.EndTime,
                p.TraceId,
            }));
        }
    }
}
