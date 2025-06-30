using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Features.MoChainTracing;

/// <summary>
/// 调用链追踪扩展方法
/// </summary>
public static class ChainTracingExtensions
{
    /// <summary>
    /// 添加调用链追踪服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddChainTracing(this IServiceCollection services)
    {
        services.AddSingleton<IMoChainTracing, AsyncLocalMoChainTracing>();
        return services;
    }

    /// <summary>
    /// 禁用调用链追踪服务（注册空实现）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection DisableChainTracing(this IServiceCollection services)
    {
        services.AddSingleton<IMoChainTracing>(_ => EmptyChainTracing.Instance);
        return services;
    }

    /// <summary>
    /// 条件性添加调用链追踪服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="enabled">是否启用调用链追踪</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddChainTracing(this IServiceCollection services, bool enabled)
    {
        if (enabled)
        {
            return services.AddChainTracing();
        }
        else
        {
            return services.DisableChainTracing();
        }
    }

    /// <summary>
    /// 条件性添加调用链追踪服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="enabledCondition">启用条件函数</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddChainTracing(this IServiceCollection services, Func<bool> enabledCondition)
    {
        return services.AddChainTracing(enabledCondition());
    }

    /// <summary>
    /// 使用调用链追踪中间件
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    /// <returns>应用程序构建器</returns>
    public static IApplicationBuilder UseChainTracing(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ChainTracingMiddleware>();
    }

    /// <summary>
    /// 执行带调用链追踪的操作
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="handler">处理者名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="action">要执行的操作</param>
    /// <param name="extraInfo">额外信息</param>
    public static async Task ExecuteWithTraceAsync(this IMoChainTracing chainTracing, 
        string handler, string operation, Func<Task> action, object? extraInfo = null)
    {
        var traceId = chainTracing.BeginTrace(handler, operation, extraInfo);
        try
        {
            await action();
            chainTracing.EndTrace(traceId, "Success", true);
        }
        catch (Exception ex)
        {
            chainTracing.RecordException(traceId, ex);
            chainTracing.EndTrace(traceId, $"Exception: {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// 执行带调用链追踪的操作并返回结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="handler">处理者名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="func">要执行的操作</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>操作结果</returns>
    public static async Task<T> ExecuteWithTraceAsync<T>(this IMoChainTracing chainTracing, 
        string handler, string operation, Func<Task<T>> func, object? extraInfo = null)
    {
        var traceId = chainTracing.BeginTrace(handler, operation, extraInfo);
        try
        {
            var result = await func();
            
            // 如果结果是 IServiceResponse，提取状态信息
            if (result is IServiceResponse serviceResponse)
            {
                chainTracing.EndTrace(traceId, $"Code: {serviceResponse.Code}", serviceResponse.Code == ResponseCode.Ok);
            }
            else
            {
                chainTracing.EndTrace(traceId, "Success", true);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            chainTracing.RecordException(traceId, ex);
            chainTracing.EndTrace(traceId, $"Exception: {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// 执行带调用链追踪的同步操作
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="handler">处理者名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="action">要执行的操作</param>
    /// <param name="extraInfo">额外信息</param>
    public static void ExecuteWithTrace(this IMoChainTracing chainTracing, 
        string handler, string operation, Action action, object? extraInfo = null)
    {
        var traceId = chainTracing.BeginTrace(handler, operation, extraInfo);
        try
        {
            action();
            chainTracing.EndTrace(traceId, "Success", true);
        }
        catch (Exception ex)
        {
            chainTracing.RecordException(traceId, ex);
            chainTracing.EndTrace(traceId, $"Exception: {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// 执行带调用链追踪的同步操作并返回结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="handler">处理者名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="func">要执行的操作</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>操作结果</returns>
    public static T ExecuteWithTrace<T>(this IMoChainTracing chainTracing, 
        string handler, string operation, Func<T> func, object? extraInfo = null)
    {
        var traceId = chainTracing.BeginTrace(handler, operation, extraInfo);
        try
        {
            var result = func();
            
            // 如果结果是 IServiceResponse，提取状态信息
            if (result is IServiceResponse serviceResponse)
            {
                chainTracing.EndTrace(traceId, $"Code: {serviceResponse.Code}", serviceResponse.Code == ResponseCode.Ok);
            }
            else
            {
                chainTracing.EndTrace(traceId, "Success", true);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            chainTracing.RecordException(traceId, ex);
            chainTracing.EndTrace(traceId, $"Exception: {ex.Message}", false);
            throw;
        }
    }

    /// <summary>
    /// 为 IServiceResponse 附加调用链信息
    /// </summary>
    /// <param name="response">服务响应</param>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="logger">日志记录器</param>
    /// <returns>服务响应（用于链式调用）</returns>
    public static T WithChainTracing<T>(this T response, IMoChainTracing chainTracing, ILogger? logger = null) 
        where T : IServiceResponse
    {
        ChainTracingResponseHelper.AttachChainToResponse(response, chainTracing, logger);
        return response;
    }

    /// <summary>
    /// 记录数据库调用
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="operation">数据库操作</param>
    /// <param name="tableName">表名</param>
    /// <param name="success">是否成功</param>
    /// <param name="duration">执行时间</param>
    /// <param name="rowsAffected">影响行数</param>
    /// <param name="extraInfo">额外信息</param>
    public static void RecordDatabaseCall(this IMoChainTracing chainTracing, 
        string operation, string? tableName = null, bool success = true, 
        TimeSpan? duration = null, int? rowsAffected = null, object? extraInfo = null)
    {
        var handler = "Database";
        var operationName = string.IsNullOrEmpty(tableName) ? operation : $"{operation}({tableName})";
        var result = success ? $"Success" : "Failed";
        
        if (rowsAffected.HasValue)
        {
            result += $", Rows: {rowsAffected}";
        }

        chainTracing.RecordTrace(handler, operationName, success, result, duration, extraInfo);
    }

    /// <summary>
    /// 记录 Redis 调用
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="operation">Redis 操作</param>
    /// <param name="key">Redis 键</param>
    /// <param name="success">是否成功</param>
    /// <param name="duration">执行时间</param>
    /// <param name="extraInfo">额外信息</param>
    public static void RecordRedisCall(this IMoChainTracing chainTracing, 
        string operation, string? key = null, bool success = true, 
        TimeSpan? duration = null, object? extraInfo = null)
    {
        var handler = "Redis";
        var operationName = string.IsNullOrEmpty(key) ? operation : $"{operation}({key})";
        var result = success ? "Success" : "Failed";

        chainTracing.RecordTrace(handler, operationName, success, result, duration, extraInfo);
    }

    /// <summary>
    /// 记录外部 API 调用
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="serviceName">服务名称</param>
    /// <param name="endpoint">API 端点</param>
    /// <param name="method">HTTP 方法</param>
    /// <param name="statusCode">响应状态码</param>
    /// <param name="success">是否成功</param>
    /// <param name="duration">执行时间</param>
    /// <param name="extraInfo">额外信息</param>
    public static void RecordExternalApiCall(this IMoChainTracing chainTracing, 
        string serviceName, string endpoint, string method = "GET", 
        int? statusCode = null, bool success = true, TimeSpan? duration = null, object? extraInfo = null)
    {
        var handler = $"ExternalAPI({serviceName})";
        var operationName = $"{method} {endpoint}";
        var result = statusCode.HasValue ? $"HTTP {statusCode}" : (success ? "Success" : "Failed");

        chainTracing.RecordTrace(handler, operationName, success, result, duration, extraInfo);
    }

    /// <summary>
    /// 记录领域服务调用
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="serviceName">服务名称</param>
    /// <param name="methodName">方法名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="duration">执行时间</param>
    /// <param name="result">调用结果</param>
    /// <param name="extraInfo">额外信息</param>
    public static void RecordDomainServiceCall(this IMoChainTracing chainTracing, 
        string serviceName, string methodName, bool success = true, 
        TimeSpan? duration = null, string? result = null, object? extraInfo = null)
    {
        var handler = $"DomainService({serviceName})";
        var finalResult = result ?? (success ? "Success" : "Failed");

        chainTracing.RecordTrace(handler, methodName, success, finalResult, duration, extraInfo);
    }

    /// <summary>
    /// 开始作用域追踪
    /// </summary>
    /// <param name="chainTracing">调用链追踪服务</param>
    /// <param name="handler">处理者名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="extraInfo">额外信息</param>
    /// <returns>作用域追踪器</returns>
    public static ChainTracingScope BeginScope(this IMoChainTracing chainTracing, 
        string handler, string operation, object? extraInfo = null)
    {
        return new ChainTracingScope(chainTracing, handler, operation, extraInfo);
    }
}