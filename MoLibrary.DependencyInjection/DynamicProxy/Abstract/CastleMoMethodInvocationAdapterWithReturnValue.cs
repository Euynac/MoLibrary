using Castle.DynamicProxy;

namespace MoLibrary.DependencyInjection.DynamicProxy.Abstract;

public class CastleMoMethodInvocationAdapterWithReturnValue<TResult>(
    IInvocation invocation,
    IInvocationProceedInfo proceedInfo,
    Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
    : CastleMoMethodInvocationAdapterBase(invocation), IMoMethodInvocation
{
    protected IInvocationProceedInfo ProceedInfo { get; } = proceedInfo;
    protected Func<IInvocation, IInvocationProceedInfo, Task<TResult>> Proceed { get; } = proceed;

    public override async Task ProceedAsync()
    {
        ReturnValue = (await Proceed(Invocation, ProceedInfo))!;
    }
}
