using Castle.DynamicProxy;
using Monica.DependencyInjection.DynamicProxy.Abstractions;

namespace Monica.DependencyInjection.DynamicProxy.Providers.Castle;

internal class CastleMethodInvocationAdapterWithReturnValue<TResult>(
    IInvocation invocation,
    Type componentType,
    IInvocationProceedInfo proceedInfo,
    Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
    : CastleMethodInvocationAdapterBase(invocation, componentType), IMethodInvocation
{
    protected IInvocationProceedInfo ProceedInfo { get; } = proceedInfo;
    protected Func<IInvocation, IInvocationProceedInfo, Task<TResult>> Proceed { get; } = proceed;

    public override async Task ProceedAsync()
    {
        ReturnValue = (await Proceed(Invocation, ProceedInfo))!;
    }
}
