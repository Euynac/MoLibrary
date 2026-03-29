using Monica.DependencyInjection.DynamicProxy.Abstract;

namespace Monica.DependencyInjection.DynamicProxy;

internal sealed record DynamicProxyInterceptorRegistration(
    Type InterceptorType,
    Func<MicrosoftDependencyInjectionDynamicProxyExtensions.ProxyBuildContext, bool> ShouldIntercept)
{
    public Type GetAdapterType()
    {
        return typeof(MoAsyncDeterminationInterceptor<>).MakeGenericType(InterceptorType);
    }
}
