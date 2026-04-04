using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Repository;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services;
using Monica.Repository.Persistence.Services.Support;
using Monica.Repository.UnitOfWork.Abstractions;
using Monica.Repository.UnitOfWork.Services;
using Monica.Repository.UnitOfWork.Services.Support;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleUnitOfWorkBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configuring the UnitOfWork module
        /// </summary>
        public static ModuleUnitOfWorkGuide AddUnitOfWork(Action<ModuleUnitOfWorkOption>? action = null)
        {
            return new ModuleUnitOfWorkGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.UnitOfWork)]
public class ModuleUnitOfWork(ModuleUnitOfWorkOption option)
    : MoModule<ModuleUnitOfWork, ModuleUnitOfWorkOption, ModuleUnitOfWorkGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        services.AddTransient<IUnitOfWork, UnitOfWork>();

        if (option.EnableEntityEvent)
        {
            services.AddTransient<IAsyncLocalEventPublisher, AsyncLocalEventPublisher>();
            services.AddTransient<IAsyncLocalEventStore, AsyncLocalEventStore>();
        }
        else
        {
            services.AddTransient<IAsyncLocalEventPublisher, NullAsyncLocalEventPublisher>();
        }

        services.AddTransient<UnitOfWorkActionFilter>();
        services.Configure<MvcOptions>(p =>
        {
            p.Filters.AddService(typeof(UnitOfWorkActionFilter));
        });
    }
}

public class ModuleUnitOfWorkGuide : MoModuleGuide<ModuleUnitOfWork, ModuleUnitOfWorkOption, ModuleUnitOfWorkGuide>
{
    public ModuleUnitOfWorkGuide AddDbContextProvider<TDbContext>() where TDbContext : RepositoryDbContext<TDbContext>
    {
        ConfigureServices(context =>
        {
            context.Services.AddTransient(typeof(IDbContextProvider<TDbContext>), typeof(UnitOfWorkDbContextProvider<TDbContext>));
            //TODO Can I use Singleton?
        }, secondKey: typeof(TDbContext).FullName);
        return this;
    }

}

public class ModuleUnitOfWorkOption : MoModuleOption<ModuleUnitOfWork>
{

    /// <summary>
    /// Enable entity change event support
    /// </summary>
    public bool EnableEntityEvent { get; set; }
}
