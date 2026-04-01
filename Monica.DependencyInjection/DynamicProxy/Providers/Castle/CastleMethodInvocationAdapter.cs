using Castle.DynamicProxy;
using Monica.DependencyInjection.DynamicProxy.Abstractions;

namespace Monica.DependencyInjection.DynamicProxy.Providers.Castle;

internal class CastleMethodInvocationAdapter(
    IInvocation invocation,
    IInvocationProceedInfo proceedInfo,
    Func<IInvocation, IInvocationProceedInfo, Task> proceed)
    : CastleMethodInvocationAdapterBase(invocation), IMethodInvocation
{
    protected IInvocationProceedInfo ProceedInfo { get; } = proceedInfo;
    protected Func<IInvocation, IInvocationProceedInfo, Task> Proceed { get; } = proceed;

    public override async Task ProceedAsync()
    {
        await Proceed(Invocation, ProceedInfo);
    }
}
