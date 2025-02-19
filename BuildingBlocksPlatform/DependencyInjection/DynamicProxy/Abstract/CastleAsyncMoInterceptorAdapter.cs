using Castle.DynamicProxy;

namespace BuildingBlocksPlatform.DependencyInjection.DynamicProxy.Abstract;

public class CastleAsyncMoInterceptorAdapter<TInterceptor>(TInterceptor interceptor) : AsyncInterceptorBase
    where TInterceptor : IMoInterceptor
{
    private readonly TInterceptor _interceptor = interceptor;

    protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
    {
        await _interceptor.InterceptAsync(
            new CastleMoMethodInvocationAdapter(invocation, proceedInfo, proceed)
        );
    }

    protected override async Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
    {
        var adapter = new CastleMoMethodInvocationAdapterWithReturnValue<TResult>(invocation, proceedInfo, proceed);

        await _interceptor.InterceptAsync(
            adapter
        );

        return (TResult)adapter.ReturnValue;
    }
}
