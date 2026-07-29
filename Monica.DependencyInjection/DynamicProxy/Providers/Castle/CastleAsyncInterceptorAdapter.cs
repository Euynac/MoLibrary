using Castle.DynamicProxy;
using Monica.DependencyInjection.DynamicProxy.Abstractions;

namespace Monica.DependencyInjection.DynamicProxy.Providers.Castle;

internal class CastleAsyncInterceptorAdapter<TInterceptor>(TInterceptor interceptor, Type componentType) : AsyncInterceptorBase
    where TInterceptor : IInvocationInterceptor
{
    private readonly TInterceptor _interceptor = interceptor;

    protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
    {
        await _interceptor.InterceptAsync(
            new CastleMethodInvocationAdapter(invocation, componentType, proceedInfo, proceed)
        );
    }

    protected override async Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
    {
        var adapter = new CastleMethodInvocationAdapterWithReturnValue<TResult>(
            invocation,
            componentType,
            proceedInfo,
            proceed);

        await _interceptor.InterceptAsync(
            adapter
        );

        return (TResult)adapter.ReturnValue;
    }
}
