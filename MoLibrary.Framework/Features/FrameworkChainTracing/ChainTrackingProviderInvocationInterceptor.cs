using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MoLibrary.Core.Features.MoChainTracing;
using MoLibrary.Core.Features.MoChainTracing.Models;
using MoLibrary.Core.Features.MoTimekeeper;
using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.DependencyInjection.DynamicProxy.Abstract;
using MoLibrary.DomainDrivenDesign.ExceptionHandler;
using MoLibrary.Framework.Features.MoRpc;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Framework.Features.FrameworkChainTracing;

public record InvocationInfo(MethodInfo MethodInfo)
{
    public string HandlerName => MethodInfo.ReflectedType?.Name
                                   ?? MethodInfo.DeclaringType?.Name
                                   ?? "Unknown";

    public string OperationName => MethodInfo.Name;

    /// <summary>
    /// 是否是远程调用，需要合并调用链
    /// </summary>
    public bool IsRemoteCall => MethodInfo.DeclaringType?.IsImplementInterface<IMoRpcApi>() is true;

    public EChainTracingType GetInvocationType()
    {
        if (IsRemoteCall) return EChainTracingType.RemoteService;
        return EChainTracingType.Unknown;
    }
}


/// <summary>
/// 基于新的 ChainTracking 系统的方法调用链追踪拦截器
/// 用于自动记录返回类型为 IServiceResponse 的方法调用链信息
/// </summary>
/// <param name="chainTracing">调用链追踪服务</param>
/// <param name="timekeeperFactory">计时器工厂</param>
/// <param name="exceptionHandler">异常处理器</param>
/// https://kozmic.net/dynamic-proxy-tutorial/
/// https://github.com/moframework/mo/issues/14378
/// https://docs.mo.io/en/mo/7.4/Dependency-Injection#advanced-features
public class ChainTrackingProviderInvocationInterceptor(
    IMoChainTracing chainTracing,
    IMoTimekeeperFactory timekeeperFactory,
    IMoExceptionHandler exceptionHandler) : MoInterceptor
{
    /// <summary>
    /// 判断是否应该记录调用链
    /// </summary>
    /// <param name="invocation">方法调用信息</param>
    /// <param name="info"></param>
    /// <returns>是否应该记录调用链</returns>
    private static bool ShouldRecordChain(IMoMethodInvocation invocation, [NotNullWhen(true)] out InvocationInfo? info)
    {
        var returnType = invocation.Method.ReturnType;
        info = null;
        // 如果是 Task<T>，获取 T 的类型
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            returnType = returnType.GetGenericArguments()[0];
        }
        
        // 判断返回类型是否实现 IServiceResponse 接口
        var shouldRecord = returnType.IsImplementInterface(typeof(IMoResponse));
        
        if (shouldRecord)
        {
            info = new InvocationInfo(invocation.Method);
            return true;
        }
        
        return false;
    }



    /// <summary>
    /// 拦截方法调用
    /// </summary>
    /// <param name="invocation">方法调用信息</param>
    public override async Task InterceptAsync(IMoMethodInvocation invocation)
    {
        if (!ShouldRecordChain(invocation, out var info))
        {
            // 不需要记录调用链的方法，直接执行
            try
            {
                await invocation.ProceedAsync();
            }
            catch (Exception ex)
            {
                // 对于不记录调用链的方法，抛出包装异常
                throw new Exception($"执行方法 {invocation.Method.DeclaringType?.Name}.{invocation.Method.Name} 异常", ex);
            }
            return;
        }

        var isRemoteCall = info.IsRemoteCall;

        // 开始调用链追踪
        using var scope =
            chainTracing.BeginScope(info.OperationName, info.HandlerName, type: info.GetInvocationType());

        // 创建计时器
        using var timer = timekeeperFactory.CreateNormalTimer(info.HandlerName);
        timer.Start();
        
        try
        {
            await invocation.ProceedAsync();
            timer.Finish();

            // 处理成功响应
            var responseTypeName = ChainTracingHelper.GetResponseTypeName(invocation.Method.ReturnType);
            
            if (invocation.ReturnValue is IMoResponse response)
            {
                var success = response.Code == ResponseCode.Ok;
                var resultDescription =
                    $"{responseTypeName}({response.Code}){(response.Message?.LimitMaxLength(1000, "...").BeNullIfWhiteSpace() is {} msg ? $"[{msg}]" : null)}";

                if (isRemoteCall)
                {
                    scope.MergeRemoteChain(response);
                }

                if (success)
                {
                    scope.EndWithSuccess(resultDescription);
                }
                else
                {
                    scope.EndWithFailure(resultDescription);
                }
            }
        }
        catch (Exception ex)
        {
            timer.Finish();

            // 记录异常到调用链
            scope.EndWithException(ex, $"执行方法 {invocation.Method.DeclaringType?.Name}.{invocation.Method.Name} 异常");

            throw;
            //if (CreateRes(invocation.Method.ReturnType) is IMoResponse exRes)
            //{
            //    var res = await exceptionHandler.TryHandleWithCurrentHttpContextAsync(ex, CancellationToken.None);
            //    exRes.ExtraInfo = res.ExtraInfo;
            //    exRes.Message = res.Message;
            //    exRes.Code = res.Code;
            //    invocation.ReturnValue = exRes;
            //}
            //else
            //{
            //    throw;
            //}
        }
    }

    private static object? CreateRes(Type type)
    {
        while (true)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                type = type.GetGenericArguments()[0];
                continue;
            }

            if (type.IsImplementInterface(typeof(IMoResponse)) && type.CanCreateInstanceUsingParameterlessConstructor())
            {
                return Activator.CreateInstance(type);
            }

            return null;
        }
    }
}