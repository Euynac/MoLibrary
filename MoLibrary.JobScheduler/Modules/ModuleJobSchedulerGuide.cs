using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.EventBus.Modules;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.StateStore.Modules;

namespace MoLibrary.JobScheduler.Modules;

/// <summary>
/// Fluent configuration builder for the Job Scheduler module.
/// </summary>
public class ModuleJobSchedulerGuide
    : MoModuleGuide<ModuleJobScheduler, ModuleJobSchedulerOption, ModuleJobSchedulerGuide>
{
    private const string CONFIG_METADATA_STORE = nameof(CONFIG_METADATA_STORE);
    private const string CONFIG_PROVIDER = nameof(CONFIG_PROVIDER);
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [CONFIG_PROVIDER,CONFIG_METADATA_STORE];
    }

    /// <summary>
    /// Configures a custom metadata repository implementation for job persistence.
    /// </summary>
    /// <typeparam name="TRepository">The metadata repository type implementing <see cref="IMoJobMetadataRepository"/>.</typeparam>
    /// <remarks>
    /// Custom repositories must be thread-safe and provide atomic state transitions.
    /// </remarks>
    public ModuleJobSchedulerGuide UseCustomMetadataRepository<TRepository>()
        where TRepository : class, IMoJobMetadataRepository
    {
        PostConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IMoJobMetadataRepository, TRepository>();
        }, key: CONFIG_METADATA_STORE);
        return this;
    }

    /// <summary>
    /// Configures the module to use the in-memory metadata repository.
    /// </summary>
    public ModuleJobSchedulerGuide UseInMemoryMetadataRepository()
    {
        PostConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IMoJobMetadataRepository, InMemoryJobMetadataRepository>();
        }, key: CONFIG_METADATA_STORE);
        return this;
    }
    
    
    /// <summary>
    /// Configures the module to use the distributed event bus and cancellation manager providers.
    /// </summary>
    /// <returns></returns>
    public ModuleJobSchedulerGuide UseDistributeProvider()
    {
        ConfigureEmpty(CONFIG_PROVIDER);
        DependsOnModule<ModuleEventBusGuide>().Register()
            .AddKeyedCommonEventBus(nameof(ModuleJobScheduler), useDistributed: true);
        DependsOnModule<ModuleCancellationManagerGuide>().Register()
            .AddKeyedCancellationManager(nameof(ModuleJobScheduler), useDistributed: true);
        DependsOnModule<ModuleRegisterCentreGuide>().Register();
        return this;
    }
   
    /// <summary>
    /// Configures the module to use the in-memory metadata store and event bus and cancellation manager providers.
    /// </summary>
    /// <returns></returns>
    public ModuleJobSchedulerGuide UseInMemoryProvider()
    {
        ConfigureEmpty(CONFIG_PROVIDER);
        DependsOnModule<ModuleEventBusGuide>().Register()
            .AddKeyedCommonEventBus(nameof(ModuleJobScheduler), useDistributed: false);
        DependsOnModule<ModuleCancellationManagerGuide>().Register()
            .AddKeyedCancellationManager(nameof(ModuleJobScheduler), useDistributed: false);
        UseInMemoryMetadataRepository();
        DependsOnModule<ModuleRegisterCentreGuide>().Register().UseInMemoryProvider();
        return this;
    }
}
