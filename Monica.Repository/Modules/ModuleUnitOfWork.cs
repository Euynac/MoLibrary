using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
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

[ModuleKey(BuiltInModuleKey.UnitOfWork)]
public class ModuleUnitOfWork(ModuleUnitOfWorkOption option)
    : ModuleBase<ModuleUnitOfWork, ModuleUnitOfWorkOption, ModuleUnitOfWorkGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();

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

public class ModuleUnitOfWorkGuide : ModuleGuide<ModuleUnitOfWork, ModuleUnitOfWorkOption, ModuleUnitOfWorkGuide>
{
    public ModuleUnitOfWorkGuide AddDbContextProvider<TDbContext>() where TDbContext : RepositoryDbContext<TDbContext>
    {
        ConfigureServices(context =>
        {
            context.Services.AddTransient(typeof(IDbContextProvider<TDbContext>), typeof(AdaptiveDbContextProvider<TDbContext>));
            //TODO Can I use Singleton?
        }, secondKey: typeof(TDbContext).FullName);
        return this;
    }

}

public class ModuleUnitOfWorkOption : ModuleOptions<ModuleUnitOfWork>
{

    /// <summary>
    /// Enable entity change event support
    /// </summary>
    public bool EnableEntityEvent { get; set; }
}
