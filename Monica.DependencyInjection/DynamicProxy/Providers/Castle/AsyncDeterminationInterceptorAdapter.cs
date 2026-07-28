using Castle.DynamicProxy;
using Monica.DependencyInjection.DynamicProxy.Abstractions;

namespace Monica.DependencyInjection.DynamicProxy.Providers.Castle;

internal class AsyncDeterminationInterceptorAdapter<TInterceptor>(TInterceptor interceptor, Type componentType)
    : AsyncDeterminationInterceptor(new CastleAsyncInterceptorAdapter<TInterceptor>(interceptor, componentType))
    where TInterceptor : IInvocationInterceptor;
