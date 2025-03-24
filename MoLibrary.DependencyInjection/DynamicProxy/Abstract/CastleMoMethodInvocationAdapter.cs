using Castle.DynamicProxy;

namespace MoLibrary.DependencyInjection.DynamicProxy.Abstract;

public class CastleMoMethodInvocationAdapter(
    IInvocation invocation,
    IInvocationProceedInfo proceedInfo,
    Func<IInvocation, IInvocationProceedInfo, Task> proceed)
    : CastleMoMethodInvocationAdapterBase(invocation), IMoMethodInvocation
{
    protected IInvocationProceedInfo ProceedInfo { get; } = proceedInfo;
    protected Func<IInvocation, IInvocationProceedInfo, Task> Proceed { get; } = proceed;

    public override async Task ProceedAsync()
    {
        await Proceed(Invocation, ProceedInfo);
    }
}
