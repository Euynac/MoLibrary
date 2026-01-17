using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Repository.Interfaces;
using MoLibrary.Repository.Transaction;
using MoLibrary.Repository.Transaction.EntityEvent;
using MoLibrary.Repository.Transaction.Interceptors;

namespace MoLibrary.Repository.Modules;

public static class ModuleUnitOfWorkBuilderExtensions
{
    public static ModuleUnitOfWorkGuide ConfigModuleUnitOfWork(this WebApplicationBuilder builder,
        Action<ModuleUnitOfWorkOption>? action = null)
    {
        return new ModuleUnitOfWorkGuide().Register(action);
    }
}

public class ModuleUnitOfWork(ModuleUnitOfWorkOption option)
    : MoModule<ModuleUnitOfWork, ModuleUnitOfWorkOption, ModuleUnitOfWorkGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.UnitOfWork;
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

public class ModuleUnitOfWorkGuide : MoModuleGuide<ModuleUnitOfWork, ModuleUnitOfWorkOption, ModuleUnitOfWorkGuide>
{
    public ModuleUnitOfWorkGuide AddDbContextProvider<TDbContext>() where TDbContext : MoDbContext<TDbContext>
    {
        ConfigureServices(context =>
        {
            context.Services.AddTransient(typeof(IDbContextProvider<TDbContext>), typeof(UnitOfWorkDbContextProvider<TDbContext>));
            //TODO 可使用Singleton？
        }, secondKey: nameof(TDbContext));
        return this;
    }

}

public class ModuleUnitOfWorkOption : MoModuleOption<ModuleUnitOfWork>
{

    /// <summary>
    /// 开启实体变更事件支持
    /// </summary>
    public bool EnableEntityEvent { get; set; }
}
