using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Repository.Transaction;
using MoLibrary.Repository.Transaction.EntityEvent;
using MoLibrary.Repository.Transaction.Interceptors;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Repository.Modules;

public class ModuleUnitOfWork(ModuleUnitOfWorkOption option)
    : MoModule<ModuleUnitOfWork, ModuleUnitOfWorkOption, ModuleUnitOfWorkGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.UnitOfWork;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMoUnitOfWorkManager, MoUnitOfWorkManager>();
        services.AddTransient<IMoUnitOfWork, MoUnitOfWork>();

        if (option.EnableEntityEvent)
        {
            services.AddTransient<IAsyncLocalEventPublisher, AsyncLocalEventPublisher>();
            services.AddTransient<IAsyncLocalEventStore, AsyncLocalEventStore>();
        }
        else
        {
            services.AddTransient<IAsyncLocalEventPublisher, NullAsyncLocalEventPublisher>();
        }

        services.AddTransient<MoActionFilterUow>();
        services.Configure<MvcOptions>(p =>
        {
            p.Filters.AddService(typeof(MoActionFilterUow));
        });
    }
}