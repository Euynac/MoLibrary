using BuildingBlocksPlatform.DependencyInjection.DynamicProxy;
using BuildingBlocksPlatform.DependencyInjection.DynamicProxy.Abstract;
using BuildingBlocksPlatform.Extensions;
using BuildingBlocksPlatform.SeedWork;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocksPlatform.Features.Decorators;

/// <summary>
/// https://kozmic.net/dynamic-proxy-tutorial/
/// https://github.com/moframework/mo/issues/14378
/// https://docs.mo.io/en/mo/7.4/Dependency-Injection#advanced-features
/// </summary>
public class InvocationChainRecorderMoInterceptor(IHttpContextAccessor accessor, IMoTimekeeper timekeeper) : MoInterceptor
{
    private bool ShouldRecordChain(IMoMethodInvocation invocation, out string? declaringType, out string? request)
    {
        if (invocation.Method.ReturnType.FullName?.Contains("BuildingBlocksPlatform.SeedWork.Res", StringComparison.Ordinal) is true)
        {
            var requestType = invocation.Arguments.FirstOrDefault()?.GetType();
            declaringType = invocation.Method.ReflectedType?.Name;
            declaringType ??= invocation.Method.DeclaringType?.Name;
            request = requestType?.Name;
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
        var context = accessor.HttpContext?.GetOrNew<OurRequestContext>();
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


        await invocation.ProceedAsync();

        if (shouldRecordChain)
        {
            keeper?.Finish();
            //https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-dynamic-expandoobject
            if (invocation.ReturnValue is IServiceResponse res)
            {
                var responseName = GetResponseName(invocation.Method.ReturnType) ?? invocation
                    .Method.ReturnType.FullName;
                //暂不支持记录远程调用，因为无法对Factory的依赖注入进行拦截。
                //if (invocation.Method.DeclaringType?.FullName?.Contains(RpcApiNamespace, StringComparison.Ordinal) is true)
                //{
                //    context.RecordRemoteCall(res);
                //}

                context.Invoked($"{responseName}({res.Code})", res: res);
            }
            else
            {
                context.ChainBridge!.Remarks = "返回值不是IServiceResponse";
            }
        }

        return;

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