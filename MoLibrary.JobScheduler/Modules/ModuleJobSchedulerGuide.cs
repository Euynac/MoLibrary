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
    /// Configures a custom metadata store implementation for job persistence.
    /// </summary>
    /// <typeparam name="TStore">The metadata store type implementing <see cref="IMoJobScheduleMetadataStore"/>.</typeparam>
    /// <remarks>
    /// Custom stores must be thread-safe and provide atomic state transitions.
    /// </remarks>
    public ModuleJobSchedulerGuide UseCustomMetadataStore<TStore>()
        where TStore : class, IMoJobScheduleMetadataStore
    {
        PostConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IMoJobScheduleMetadataStore, TStore>();
        }, key: CONFIG_METADATA_STORE);
        return this;
    }

    /// <summary>
    /// Configures the module to use the in-memory metadata store provider.
    /// </summary>
    public ModuleJobSchedulerGuide UseInMemoryMetadataStore()
    {
        PostConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IMoJobScheduleMetadataStore, MetadataStoreInMemoryProvider>();
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
        UseInMemoryMetadataStore();
        DependsOnModule<ModuleRegisterCentreGuide>().Register().UseInMemoryProvider();
        return this;
    }
}
