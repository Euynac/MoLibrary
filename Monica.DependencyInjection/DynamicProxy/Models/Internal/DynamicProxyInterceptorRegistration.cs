using Monica.DependencyInjection.DynamicProxy.Providers.Castle;

namespace Monica.DependencyInjection.DynamicProxy.Models.Internal;

internal sealed record DynamicProxyInterceptorRegistration(
    Type InterceptorType,
    Func<ProxyBuildContext, bool> ShouldIntercept)
{
    public Type GetAdapterType()
    {
        return typeof(AsyncDeterminationInterceptorAdapter<>).MakeGenericType(InterceptorType);
    }
}
