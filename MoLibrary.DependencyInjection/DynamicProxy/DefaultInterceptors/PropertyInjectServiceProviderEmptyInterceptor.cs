using MoLibrary.DependencyInjection.DynamicProxy.Abstract;

namespace MoLibrary.DependencyInjection.DynamicProxy.DefaultInterceptors;

public class PropertyInjectServiceProviderEmptyInterceptor : MoInterceptor
{
    public override async Task InterceptAsync(IMoMethodInvocation invocation)
    {
        await invocation.ProceedAsync();
    }
}