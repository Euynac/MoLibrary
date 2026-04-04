using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Repository.UnitOfWork.Abstractions;
using Monica.Repository.UnitOfWork.Services;
using Monica.Repository.UnitOfWork.Services.Support;

namespace Monica.Repository.UnitOfWork.Extensions;

public static class ServiceCollectionUnitOfWorkExtensions
{
    public static IServiceCollection AddUnitOfWorkServices(this IServiceCollection services)
    {
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        services.AddTransient<IUnitOfWork, Services.UnitOfWork>();

        services.TryAddTransient<IAsyncLocalEventPublisher, NullAsyncLocalEventPublisher>();
        services.AddTransient<UnitOfWorkActionFilter>();
        services.Configure<MvcOptions>(p =>
        {
            p.Filters.AddService(typeof(UnitOfWorkActionFilter));
        });
        return services;
    }
    public static IServiceCollection AddUnitOfWorkServicesWithEvents(this IServiceCollection services)
    {
        AddUnitOfWorkServices(services);
        services.Replace(ServiceDescriptor.Transient<IAsyncLocalEventPublisher, AsyncLocalEventPublisher>());
        services.AddTransient<IAsyncLocalEventStore, AsyncLocalEventStore>();
        return services;
    }
}