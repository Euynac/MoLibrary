using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Execution;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
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
        public ModuleRegistration<ModuleUnitOfWork, ModuleUnitOfWorkOption> AddUnitOfWork(Action<ModuleUnitOfWorkOption>? action = null)
        {
            var module = builder.AddModule<ModuleUnitOfWork, ModuleUnitOfWorkOption>(action);
            module.Require<ModuleExecutionPipeline, ModuleExecutionPipelineOption>()
                .AddBehavior(
                    typeof(UnitOfWorkExecutionBehavior<,>),
                    ExecutionBehaviorOrder.UnitOfWork,
                    static descriptor => descriptor.TransactionMode == ExecutionTransactionMode.Automatic);
            return module;
        }
    }
}

public class ModuleUnitOfWork : MonicaModule<ModuleUnitOfWorkOption>
{
    public override void ConfigureServices(ModuleContext<ModuleUnitOfWorkOption> context)
    {
        var services = context.Services;
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

    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleDependencyInjection, ModuleDependencyInjectionOption>();
        module.Require<ModuleExecutionPipeline, ModuleExecutionPipelineOption>();
    }
}

public static class ModuleUnitOfWorkRegistrationExtensions
{
    public static ModuleRegistration<ModuleUnitOfWork, ModuleUnitOfWorkOption> AddDbContextProvider<TDbContext>(this ModuleRegistration<ModuleUnitOfWork, ModuleUnitOfWorkOption> module) where TDbContext : RepositoryDbContext<TDbContext>
    {
        module.ConfigureServices(context =>
        {
            context.Services.AddTransient(
                typeof(IDbContextProvider<TDbContext>),
                typeof(AdaptiveDbContextProvider<TDbContext>));
        });
        return module;
    }

}

public class ModuleUnitOfWorkOption : ModuleOptions<ModuleUnitOfWork>
{
    /// <summary>
    /// Enable entity change event support
    /// </summary>
    public bool EnableEntityEvent { get; set; }
}
