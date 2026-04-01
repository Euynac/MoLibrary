using Castle.DynamicProxy;
using Monica.DependencyInjection.DynamicProxy.Abstractions;

namespace Monica.DependencyInjection.DynamicProxy.Providers.Castle;

internal class AsyncDeterminationInterceptorAdapter<TInterceptor>(TInterceptor interceptor)
    : AsyncDeterminationInterceptor(new CastleAsyncInterceptorAdapter<TInterceptor>(interceptor))
    where TInterceptor : IInvocationInterceptor;
