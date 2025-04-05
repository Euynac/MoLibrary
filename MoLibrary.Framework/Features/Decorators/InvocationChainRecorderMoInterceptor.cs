using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Features;
using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.DependencyInjection.DynamicProxy.Abstract;
using MoLibrary.DomainDrivenDesign.ExceptionHandler;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Framework.Features.Decorators;

/// <summary>
/// https://kozmic.net/dynamic-proxy-tutorial/
/// https://github.com/moframework/mo/issues/14378
/// https://docs.mo.io/en/mo/7.4/Dependency-Injection#advanced-features
/// </summary>
public class InvocationChainRecorderMoInterceptor(IHttpContextAccessor accessor, IMoTimekeeper timekeeper, IMoExceptionHandler exceptionHandler) : MoInterceptor
{
    private bool ShouldRecordChain(IMoMethodInvocation invocation, out string? declaringType, out string? request)
    {
        if (invocation.Method.ReturnType.FullName?.Contains(nameof(Tool.MoResponse), StringComparison.Ordinal) is true)
        {
            var requestType = invocation.Arguments.FirstOrDefault()?.GetType();
            declaringType = invocation.Method.ReflectedType?.Name;
            declaringType ??= invocation.Method.DeclaringType?.Name;
            request = requestType?.Name;
            //TODO 这里可配置
            if (!requestType?.FullName?.Contains("ProtocolPlatform.PublishedLanguages", StringComparison.Ordinal) is true)
            {
                request = invocation.Method.Name;
            }

            return true;
        }

        declaringType = null;
        request = null;
        return false;
    }

    public override async Task InterceptAsync(IMoMethodInvocation invocation)
    {
        var shouldRecordChain = ShouldRecordChain(invocation, out var declaringType, out var request);
        var context = accessor.HttpContext?.GetOrNew<MoRequestContext>();
        if (context is null)
        {
            await invocation.ProceedAsync();
            return;
        }

        NormalTimekeeper? keeper = null;
        if (shouldRecordChain)
        {
            context.Invoking(declaringType ?? "", request ?? "");
            if (declaringType is { } key)
            {
                keeper = timekeeper.CreateNormalTimer(key);
                keeper.Start();
            }
        }

        Exception? exception = null;
        try
        {
            await invocation.ProceedAsync();
        }
        catch (Exception e)
        {
            exception = e;
        }


        if (shouldRecordChain)
        {
            keeper?.Finish();
            var responseName = GetResponseName(invocation.Method.ReturnType) ?? invocation
                .Method.ReturnType.FullName;
            //https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-dynamic-expandoobject
            if (invocation.ReturnValue is IServiceResponse res)
            {
                context.Invoked($"{responseName}({res.Code})", res: res);
            }
            else if(exception != null && CreateRes(invocation.Method.ReturnType) is IServiceResponse errorRes)
            {
                var handledRes = await exceptionHandler.TryHandleAsync(accessor.HttpContext, exception, CancellationToken.None);
                handledRes.AppendExtraInfo("exception", $"执行方法{invocation.Method.DeclaringType?.Name} {invocation.Method.Name} 异常");
                errorRes.MergeRes(handledRes);
                invocation.ReturnValue = errorRes;
                context.Invoked($"{responseName}({errorRes.Code})", res: errorRes);
            }
            else
            {
                //TODO 未考虑的情况
            }
        }
        else if(exception != null)
        {
            throw new Exception($"执行方法{invocation.Method.DeclaringType?.Name} {invocation.Method.Name} 异常", exception);
        }

        return;

        static object? CreateRes(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                return CreateRes(type.GetGenericArguments()[0]);
            }

            if (type.IsImplementInterface(typeof(IServiceResponse)) && type.CanCreateInstanceUsingParameterlessConstructor())
            {
                return Activator.CreateInstance(type);
            }

            return null;
        }

        static string? GetResponseName(Type type)
        {
            if (type.IsGenericType)
            {
                var tmp = type.GenericTypeArguments.FirstOrDefault();
                while (tmp?.IsGenericType is true)
                {
                    tmp = tmp.GenericTypeArguments.FirstOrDefault();
                }

                return tmp?.Name ?? type.Name;
            }
            return type.Name;
        }
    }
}