using System.Dynamic;
using BuildingBlocksPlatform.DependencyInjection.DynamicProxy;
using BuildingBlocksPlatform.DependencyInjection.DynamicProxy.Abstract;
using BuildingBlocksPlatform.Extensions;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocksPlatform.Features.Decorators;

public class InvocationChainSlowlyMethodDetectorInterceptor(ILogger<InvocationChainSlowlyMethodDetectorInterceptor> logger, IHttpContextAccessor accessor) : MoInterceptor
{
   
    public override async Task InterceptAsync(IMoMethodInvocation invocation)
    {   
        var context = accessor.HttpContext?.GetOrNew<OurRequestContext>();
        if (context is null)
        {
            await invocation.ProceedAsync();
            return;
        }
        var start = DateTime.Now;
        await invocation.ProceedAsync();
        var diff = (DateTime.Now - start).TotalSeconds;
        if (diff > 1)
        {
            context.OtherInfo ??= new ExpandoObject();
            context.OtherInfo.Append("warning", new
            {
                MethodName = invocation.Method.Name,
                Info = $"执行超时警告：用时{diff:0.##}秒",
            });
        }
    }
}