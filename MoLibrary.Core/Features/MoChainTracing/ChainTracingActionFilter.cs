using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Features.MoChainTracing;

/// <summary>
/// 调用链追踪 Action Filter
/// 自动将调用链信息附加到控制器返回的 IServiceResponse 中
/// </summary>
public class ChainTracingActionFilter : IActionFilter, IResultFilter
{
    private readonly IMoChainTracing _chainTracing;
    private readonly ILogger<ChainTracingActionFilter> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="logger">日志记录器</param>
    public ChainTracingActionFilter(IMoChainTracing chainTracing, ILogger<ChainTracingActionFilter> logger)
    {
        _chainTracing = chainTracing;
        _logger = logger;
    }

    /// <summary>
    /// Action 执行前
    /// </summary>
    /// <param name="context">Action 执行上下文</param>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var controllerName = context.Controller.GetType().Name;
        var actionName = context.ActionDescriptor.DisplayName ?? context.ActionDescriptor.RouteValues["action"] ?? "Unknown";
        
        // 记录控制器 Action 的开始
        var actionTraceId = _chainTracing.BeginTrace($"Controller({controllerName})", actionName, new
        {
            ControllerName = controllerName,
            ActionName = actionName,
            Parameters = context.ActionArguments?.Keys.ToArray(),
            RouteValues = context.ActionDescriptor.RouteValues
        });

        // 将 TraceId 存储到 ActionContext 中，以便在 OnActionExecuted 中使用
        context.HttpContext.Items["ChainTracingActionTraceId"] = actionTraceId;
        
        _logger.LogDebug("开始执行 Controller Action: {ControllerName}.{ActionName}, TraceId: {TraceId}", 
            controllerName, actionName, actionTraceId);
    }

    /// <summary>
    /// Action 执行后
    /// </summary>
    /// <param name="context">Action 执行上下文</param>
    public void OnActionExecuted(ActionExecutedContext context)
    {
        var actionTraceId = context.HttpContext.Items["ChainTracingActionTraceId"]?.ToString();
        if (string.IsNullOrEmpty(actionTraceId))
        {
            return;
        }

        var controllerName = context.Controller.GetType().Name;
        var actionName = context.ActionDescriptor.DisplayName ?? context.ActionDescriptor.RouteValues["action"] ?? "Unknown";

        try
        {
            if (context.Exception != null)
            {
                // 记录异常
                _chainTracing.RecordException(actionTraceId, context.Exception);
                _chainTracing.EndTrace(actionTraceId, $"Exception: {context.Exception.Message}", false);
                
                _logger.LogError(context.Exception, "Controller Action 执行异常: {ControllerName}.{ActionName}", 
                    controllerName, actionName);
            }
            else
            {
                // 检查返回结果
                var result = ExtractResult(context.Result);
                if (result is IServiceResponse serviceResponse)
                {
                    var success = serviceResponse.Code == ResponseCode.Ok;
                    _chainTracing.EndTrace(actionTraceId, $"Code: {serviceResponse.Code}", success);
                    
                    _logger.LogDebug("Controller Action 执行完成: {ControllerName}.{ActionName}, Code: {Code}", 
                        controllerName, actionName, serviceResponse.Code);
                }
                else
                {
                    _chainTracing.EndTrace(actionTraceId, "Success", true);
                    
                    _logger.LogDebug("Controller Action 执行完成: {ControllerName}.{ActionName}", 
                        controllerName, actionName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 Controller Action 调用链时发生异常: {ControllerName}.{ActionName}", 
                controllerName, actionName);
        }
    }

    /// <summary>
    /// Result 执行前
    /// </summary>
    /// <param name="context">Result 执行上下文</param>
    public void OnResultExecuting(ResultExecutingContext context)
    {
        // 在这里可以进一步处理结果
    }

    /// <summary>
    /// Result 执行后
    /// </summary>
    /// <param name="context">Result 执行上下文</param>
    public void OnResultExecuted(ResultExecutedContext context)
    {
        try
        {
            // 尝试将调用链信息附加到响应中
            var result = ExtractResult(context.Result);
            if (result is IServiceResponse serviceResponse)
            {
                ChainTracingResponseHelper.AttachChainToResponse(serviceResponse, _chainTracing, _logger);
                
                _logger.LogDebug("成功将调用链信息附加到响应");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "附加调用链信息到响应时发生异常");
        }
    }

    /// <summary>
    /// 从 ActionResult 中提取实际的结果对象
    /// </summary>
    /// <param name="result">Action 结果</param>
    /// <returns>实际的结果对象</returns>
    private static object? ExtractResult(IActionResult? result)
    {
        return result switch
        {
            ObjectResult objectResult => objectResult.Value,
            JsonResult jsonResult => jsonResult.Value,
            ContentResult contentResult => contentResult.Content,
            _ => result
        };
    }
}

/// <summary>
/// 调用链追踪 Action Filter 特性
/// 可以直接应用到控制器或 Action 上
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ChainTracingAttribute : Attribute, IFilterFactory
{
    /// <summary>
    /// 是否可重用
    /// </summary>
    public bool IsReusable => false;

    /// <summary>
    /// 创建过滤器实例
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>过滤器实例</returns>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var chainTracing = serviceProvider.GetRequiredService<IMoChainTracing>();
        var logger = serviceProvider.GetRequiredService<ILogger<ChainTracingActionFilter>>();
        return new ChainTracingActionFilter(chainTracing, logger);
    }
}

/// <summary>
/// 自动调用链追踪 Action Filter
/// 用于全局注册，自动处理所有控制器的调用链追踪
/// </summary>
public class AutoChainTracingActionFilter : IActionFilter, IResultFilter
{
    private readonly IMoChainTracing _chainTracing;
    private readonly ILogger<AutoChainTracingActionFilter> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="logger">日志记录器</param>
    public AutoChainTracingActionFilter(IMoChainTracing chainTracing, ILogger<AutoChainTracingActionFilter> logger)
    {
        _chainTracing = chainTracing;
        _logger = logger;
    }

    /// <summary>
    /// Action 执行前
    /// </summary>
    /// <param name="context">Action 执行上下文</param>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // 检查是否应该跳过调用链追踪
        if (ShouldSkipTracing(context))
        {
            return;
        }

        var controllerName = context.Controller.GetType().Name;
        var actionName = context.ActionDescriptor.DisplayName ?? context.ActionDescriptor.RouteValues["action"] ?? "Unknown";
        
        var actionTraceId = _chainTracing.BeginTrace($"Controller({controllerName})", actionName, new
        {
            ControllerName = controllerName,
            ActionName = actionName,
            Parameters = context.ActionArguments?.Keys.ToArray(),
            RouteValues = context.ActionDescriptor.RouteValues
        });

        context.HttpContext.Items["AutoChainTracingActionTraceId"] = actionTraceId;
    }

    /// <summary>
    /// Action 执行后
    /// </summary>
    /// <param name="context">Action 执行上下文</param>
    public void OnActionExecuted(ActionExecutedContext context)
    {
        var actionTraceId = context.HttpContext.Items["AutoChainTracingActionTraceId"]?.ToString();
        if (string.IsNullOrEmpty(actionTraceId))
        {
            return;
        }

        try
        {
            if (context.Exception != null)
            {
                _chainTracing.RecordException(actionTraceId, context.Exception);
                _chainTracing.EndTrace(actionTraceId, $"Exception: {context.Exception.Message}", false);
            }
            else
            {
                var result = ExtractResult(context.Result);
                if (result is IServiceResponse serviceResponse)
                {
                    var success = serviceResponse.Code == ResponseCode.Ok;
                    _chainTracing.EndTrace(actionTraceId, $"Code: {serviceResponse.Code}", success);
                }
                else
                {
                    _chainTracing.EndTrace(actionTraceId, "Success", true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自动调用链追踪处理异常");
        }
    }

    /// <summary>
    /// Result 执行前
    /// </summary>
    /// <param name="context">Result 执行上下文</param>
    public void OnResultExecuting(ResultExecutingContext context)
    {
        // 无需处理
    }

    /// <summary>
    /// Result 执行后
    /// </summary>
    /// <param name="context">Result 执行上下文</param>
    public void OnResultExecuted(ResultExecutedContext context)
    {
        try
        {
            var actionTraceId = context.HttpContext.Items["AutoChainTracingActionTraceId"]?.ToString();
            if (string.IsNullOrEmpty(actionTraceId))
            {
                return;
            }

            var result = ExtractResult(context.Result);
            if (result is IServiceResponse serviceResponse)
            {
                ChainTracingResponseHelper.AttachChainToResponse(serviceResponse, _chainTracing, _logger);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自动调用链追踪附加响应信息异常");
        }
    }

    /// <summary>
    /// 检查是否应该跳过调用链追踪
    /// </summary>
    /// <param name="context">Action 执行上下文</param>
    /// <returns>是否跳过</returns>
    private static bool ShouldSkipTracing(ActionExecutingContext context)
    {
        // 检查是否有 SkipChainTracing 特性
        var controllerType = context.Controller.GetType();
        var actionMethod = context.ActionDescriptor.RouteValues["action"];
        
        // 这里可以添加更多的跳过逻辑
        // 例如：健康检查端点、静态资源等
        var requestPath = context.HttpContext.Request.Path.Value?.ToLower();
        if (requestPath != null && (requestPath.Contains("/health") || requestPath.Contains("/swagger")))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 从 ActionResult 中提取实际的结果对象
    /// </summary>
    /// <param name="result">Action 结果</param>
    /// <returns>实际的结果对象</returns>
    private static object? ExtractResult(IActionResult? result)
    {
        return result switch
        {
            ObjectResult objectResult => objectResult.Value,
            JsonResult jsonResult => jsonResult.Value,
            ContentResult contentResult => contentResult.Content,
            _ => result
        };
    }
}

/// <summary>
/// 跳过调用链追踪特性
/// 可以应用到控制器或 Action 上，指示跳过调用链追踪
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SkipChainTracingAttribute : Attribute
{
} 