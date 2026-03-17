using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Repository;
using Monica.Repository.Interfaces;
using Monica.Repository.Transaction;
using Monica.Repository.Transaction.EntityEvent;
using Monica.Repository.Transaction.Interceptors;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleUnitOfWorkBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 UnitOfWork 模块
        /// </summary>
        public static ModuleUnitOfWorkGuide AddUnitOfWork(Action<ModuleUnitOfWorkOption>? action = null)
        {
            return new ModuleUnitOfWorkGuide().Register(action);
        }
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
        }, secondKey: typeof(TDbContext).FullName);
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
