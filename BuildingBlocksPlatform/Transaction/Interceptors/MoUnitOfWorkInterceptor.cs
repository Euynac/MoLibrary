using BuildingBlocksPlatform.DependencyInjection.DynamicProxy;
using BuildingBlocksPlatform.DependencyInjection.DynamicProxy.Abstract;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.Transaction.Interceptors;

public class MoUnitOfWorkInterceptor(IServiceScopeFactory serviceScopeFactory) : MoInterceptor
{

    public override async Task InterceptAsync(IMoMethodInvocation invocation)
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
