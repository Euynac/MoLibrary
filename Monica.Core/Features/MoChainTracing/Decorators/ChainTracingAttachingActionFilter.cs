using System.Dynamic;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Monica.Core.Features.MoChainTracing.Models;
using Monica.Modules;
using Monica.Tool.Extensions;
using Monica.Tool.Results;

namespace Monica.Core.Features.MoChainTracing.Decorators;

/// <summary>
/// Attaches chain data to controller responses that implement <see cref="IResultEnvelope" />.
/// </summary>
public class ChainTracingAttachingActionFilter(IMoChainTracing chainTracing, IOptions<ModuleChainTracingOption> options) : IActionFilter
{
    public ModuleChainTracingOption Options { get; } = options.Value;

    /// <summary>
    /// Runs before the action executes.
    /// </summary>
    /// <param name="context">The action execution context.</param>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        
    }

    /// <summary>
    /// Runs after the action executes.
    /// </summary>
    /// <param name="context">The action execution context.</param>
    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Attach chain metadata only when the action returned an IResultEnvelope.
        if (chainTracing.GetCurrentChain() is { } chain && ChainTracingHelper.ExtractResult(context.Result) is IResultEnvelope serviceResponse)
        {
            chain.MarkComplete();
            serviceResponse.Metadata ??= new ExpandoObject();
            serviceResponse.Metadata.Append(MoChainContext.CHAIN_KEY, chain.Root);
            if(chain.IsolatedNodes is not null)
            {
                serviceResponse.Metadata.Append(MoChainContext.CHAIN_KEY+"_error", chain.IsolatedNodes.Select(p => new
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
}
