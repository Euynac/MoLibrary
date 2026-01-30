using Monica.DependencyInjection.DynamicProxy.Abstract;

namespace Monica.DependencyInjection.DynamicProxy.DefaultInterceptors;

public class PropertyInjectServiceProviderEmptyInterceptor : MoInterceptor
{
    public override async Task InterceptAsync(IMoMethodInvocation invocation)
    {
        await invocation.ProceedAsync();
    }
}