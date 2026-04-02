using Microsoft.Extensions.DependencyInjection;
using Monica.DependencyInjection.DynamicProxy.Abstractions;

namespace Monica.Repository.Transaction.Interceptors;

public class MoUnitOfWorkInterceptor(IServiceScopeFactory serviceScopeFactory) : InvocationInterceptor
{

    public override async Task InterceptAsync(IMethodInvocation invocation)
    {
        //if (!UnitOfWorkHelper.IsUnitOfWorkMethod(invocation.Method, out var unitOfWorkAttribute))
        //{
        //    await processAction.Invoke();
        //    return;
        //}

        using var scope = serviceScopeFactory.CreateScope();
        var options = new MoUnitOfWorkOptions();

        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IMoUnitOfWorkManager>();

        using var uow = unitOfWorkManager.Begin(options);
        await invocation.ProceedAsync();
        await uow.CompleteAsync();
    }
}
