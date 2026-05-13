using Microsoft.Extensions.DependencyInjection;
using Monica.DependencyInjection.DynamicProxy.Abstractions;
using Monica.Repository.UnitOfWork.Abstractions;

namespace Monica.Repository.UnitOfWork.Services.Support;

public class UnitOfWorkInterceptor(IServiceScopeFactory serviceScopeFactory) : InvocationInterceptor
{

    public override async Task InterceptAsync(IMethodInvocation invocation)
    {
        //if (!UnitOfWorkHelper.IsUnitOfWorkMethod(invocation.Method, out var unitOfWorkAttribute))
        //{
        //    await processAction.Invoke();
        //    return;
        //}

        using var scope = serviceScopeFactory.CreateScope();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        await using var uow = unitOfWorkManager.BeginScope();
        await invocation.ProceedAsync();
        await uow.CompleteAsync();
    }
}
