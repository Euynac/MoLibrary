using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Execution;
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
using Monica.Repository.UnitOfWork.Services.Behaviors;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleUnitOfWorkBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configuring the UnitOfWork module
        /// </summary>
        public ModuleUnitOfWorkGuide AddUnitOfWork(Action<ModuleUnitOfWorkOption>? action = null)
        {
            return builder.AddModule<ModuleUnitOfWork, ModuleUnitOfWorkOption, ModuleUnitOfWorkGuide>(action);
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

        if (Option.EnableEntityEvent)
        {
            services.AddTransient<IAsyncLocalEventPublisher, AsyncLocalEventPublisher>();
            services.AddTransient<IAsyncLocalEventStore, AsyncLocalEventStore>();
        }
        else
        {
            services.AddTransient<IAsyncLocalEventPublisher, NullAsyncLocalEventPublisher>();
        }
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleDependencyInjectionGuide>().Register();
        DependsOnModule<ModuleExecutionPipelineGuide>().Register()
            .AddBehavior(
                typeof(UnitOfWorkExecutionBehavior<,>),
                ExecutionBehaviorOrder.UnitOfWork,
                static descriptor => descriptor.TransactionMode == ExecutionTransactionMode.Automatic);
    }
}

public class ModuleUnitOfWorkGuide : ModuleGuide<ModuleUnitOfWork, ModuleUnitOfWorkOption, ModuleUnitOfWorkGuide>
{
    public ModuleUnitOfWorkGuide AddDbContextProvider<TDbContext>() where TDbContext : RepositoryDbContext<TDbContext>
    {
        ConfigureServices(context =>
        {
            context.Services.AddTransient(
                typeof(IDbContextProvider<TDbContext>),
                typeof(AdaptiveDbContextProvider<TDbContext>));
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
