using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.DependencyInjection.DynamicProxy.Abstract;

namespace BuildingBlocksPlatform.Features.Decorators;

public class PropertyInjectServiceProviderEmptyInterceptor : MoInterceptor
{
    public override async Task InterceptAsync(IMoMethodInvocation invocation)
    {
        await invocation.ProceedAsync();
    }
}