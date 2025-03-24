using MoLibrary.DependencyInjection.DynamicProxy.Abstract;

namespace MoLibrary.DependencyInjection.DynamicProxy;

public abstract class MoInterceptor : IMoInterceptor
{
    public abstract Task InterceptAsync(IMoMethodInvocation invocation);
}



#region 方案二


///// <summary>
///// Abstract base class for asynchronous interceptors using Castle DynamicProxy.
///// Provides methods to intercept synchronous and asynchronous method invocations.
///// </summary>
//public abstract class MoAsyncInterceptor : IAsyncInterceptor
//{
//    /// <summary>
//    /// Abstract method to handle interception logic for asynchronous method invocations.
//    /// </summary>
//    /// <param name="invocation">The invocation being intercepted.</param>
//    /// <param name="processAction">The action to process the invocation.</param>
//    /// <returns>A task representing the asynchronous operation.</returns>
//    public abstract Task Intercept(IInvocation invocation, Func<Task> processAction);
//    /// <summary>
//    /// Intercepts synchronous method invocations.
//    /// </summary>
//    /// <param name="invocation">The invocation being intercepted.</param>
//    public void InterceptSynchronous(IInvocation invocation)
//    {
//        Intercept(invocation, () =>
//        {
//            invocation.Proceed();
//            return Task.CompletedTask;
//        });
//    }
//    /// <summary>
//    /// Intercepts asynchronous method invocations without a return value.
//    /// </summary>
//    /// <param name="invocation">The invocation being intercepted.</param>
//    public void InterceptAsynchronous(IInvocation invocation)
//    {
//        invocation.ReturnValue = InternalInterceptAsynchronous(invocation);
//    }
//    /// <summary>
//    /// Internal method to handle asynchronous interception logic without a return value.
//    /// </summary>
//    /// <param name="invocation">The invocation being intercepted.</param>
//    /// <returns>A task representing the asynchronous operation.</returns>
//    private async Task InternalInterceptAsynchronous(IInvocation invocation)
//    {
//        await Intercept(invocation, async () =>
//        {
//            invocation.Proceed();
//            var task = (Task) invocation.ReturnValue;
//            await task;
//        });
//    }
//    /// <summary>
//    /// Intercepts asynchronous method invocations with a return value.
//    /// </summary>
//    /// <typeparam name="TResult">The type of the return value.</typeparam>
//    /// <param name="invocation">The invocation being intercepted.</param>
//    public void InterceptAsynchronous<TResult>(IInvocation invocation)
//    {
//        invocation.ReturnValue = InternalInterceptAsynchronous<TResult>(invocation);
//    }
//    /// <summary>
//    /// Internal method to handle asynchronous interception logic with a return value.
//    /// </summary>
//    /// <typeparam name="TResult">The type of the return value.</typeparam>
//    /// <param name="invocation">The invocation being intercepted.</param>
//    /// <returns>A task representing the asynchronous operation with a result.</returns>
//    private async Task<TResult> InternalInterceptAsynchronous<TResult>(IInvocation invocation)
//    {
//        TResult? result = default;
//        await Intercept(invocation, async () =>
//        {
//            invocation.Proceed();
//            var task = (Task<TResult>) invocation.ReturnValue;
//            result = await task;
//        });
//        return result!;
//    }
//}


#endregion
