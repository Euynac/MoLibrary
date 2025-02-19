using BuildingBlocksPlatform.Transaction.EntityEvent;
using BuildingBlocksPlatform.Transaction.Interceptors;
using Google.Rpc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocksPlatform.Transaction;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddMoUnitOfWork(this IServiceCollection services)
    {
        services.AddSingleton<IMoUnitOfWorkManager, MoUnitOfWorkManager>();
        services.AddTransient<IMoUnitOfWork, MoUnitOfWork>();

        services.TryAddTransient<IAsyncLocalEventPublisher, NullAsyncLocalEventPublisher>();
        services.AddTransient<MoActionFilterUow>();
        services.Configure<MvcOptions>(p =>
        {
            p.Filters.AddService(typeof(MoActionFilterUow));
        });
        return services;
    }
    public static IServiceCollection AddMoUnitOfWorkWithEvent(this IServiceCollection services)
    {
        AddMoUnitOfWork(services);
        services.Replace(ServiceDescriptor.Transient<IAsyncLocalEventPublisher, AsyncLocalEventPublisher>());
        services.AddTransient<IAsyncLocalEventStore, AsyncLocalEventStore>();
        return services;
    }
}