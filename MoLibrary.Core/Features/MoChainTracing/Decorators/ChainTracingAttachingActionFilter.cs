using System.Dynamic;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Modules;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Features.MoChainTracing.Decorators;

/// <summary>
/// 自动将调用链信息附加到控制器返回的 IServiceResponse 中
/// </summary>
public class ChainTracingAttachingActionFilter(IMoChainTracing chainTracing, IOptions<ModuleChainTracingOption> options) : IActionFilter
{
    public ModuleChainTracingOption Options { get; } = options.Value;

    /// <summary>
    /// Action 执行前
    /// </summary>
    /// <param name="context">Action 执行上下文</param>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        
    }

    /// <summary>
    /// Action 执行后
    /// </summary>
    /// <param name="context">Action 执行上下文</param>
    public void OnActionExecuted(ActionExecutedContext context)
    {
        // 检查返回结果
        if (chainTracing.GetCurrentChain() is { } chain && ChainTracingHelper.ExtractResult(context.Result) is IServiceResponse serviceResponse)
        {
            chain.MarkComplete();
            serviceResponse.ExtraInfo ??= new ExpandoObject();
            serviceResponse.ExtraInfo.Append("chain", chain.Root);
        }
    }
}