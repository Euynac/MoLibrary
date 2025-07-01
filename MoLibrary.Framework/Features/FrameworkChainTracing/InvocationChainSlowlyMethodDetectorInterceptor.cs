using System.Dynamic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Features;
using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.DependencyInjection.DynamicProxy.Abstract;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Features.FrameworkChainTracing;

public class InvocationChainSlowlyMethodDetectorInterceptor(ILogger<InvocationChainSlowlyMethodDetectorInterceptor> logger, IHttpContextAccessor accessor) : MoInterceptor
{
   
    public override async Task InterceptAsync(IMoMethodInvocation invocation)
    {   
        var context = accessor.HttpContext?.GetOrNew<MoRequestContext>();
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