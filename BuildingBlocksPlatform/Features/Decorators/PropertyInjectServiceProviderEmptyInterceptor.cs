using BuildingBlocksPlatform.DependencyInjection.DynamicProxy;
using BuildingBlocksPlatform.DependencyInjection.DynamicProxy.Abstract;

namespace BuildingBlocksPlatform.Features.Decorators;

public class PropertyInjectServiceProviderEmptyInterceptor : MoInterceptor
{
    public override async Task InterceptAsync(IMoMethodInvocation invocation)
    {
        await invocation.ProceedAsync();
    }
}