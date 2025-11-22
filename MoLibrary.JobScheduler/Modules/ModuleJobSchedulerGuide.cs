using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.EventBus.Modules;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.StateStore.Modules;

namespace MoLibrary.JobScheduler.Modules;

/// <summary>
/// Fluent configuration builder for the Job Scheduler module.
/// </summary>
public class ModuleJobSchedulerGuide
    : MoModuleGuide<ModuleJobScheduler, ModuleJobSchedulerOption, ModuleJobSchedulerGuide>
{
    private const string ConfigMetadataStore = nameof(ConfigMetadataStore);
    private const string ConfigProvider = nameof(ConfigProvider);
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [ConfigProvider,ConfigMetadataStore];
    }

    /// <summary>
    /// Configures a custom metadata store implementation for job persistence.
    /// </summary>
    /// <typeparam name="TStore">The metadata store type implementing <see cref="IMoJobScheduleMetadataStore"/>.</typeparam>\
    /// <remarks>
    /// Custom stores must be thread-safe and provide atomic state transitions.
    /// </remarks>
    public ModuleJobSchedulerGuide UseCustomMetadataStore<TStore>()
        where TStore : class, IMoJobScheduleMetadataStore
    {
        PostConfigureServices(context =>
        {
            context.Services.AddSingleton<IMoJobScheduleMetadataStore, TStore>();
        }, key: ConfigMetadataStore);
        return this;
    }

    /// <summary>
    /// Configures the module to use the in-memory metadata store provider.
    /// </summary>
    public ModuleJobSchedulerGuide UseInMemoryMetadataStore()
    {
        PostConfigureServices(context =>
        {
            context.Services.AddSingleton<IMoJobScheduleMetadataStore, MetadataStoreInMemoryProvider>();
        }, key: ConfigMetadataStore);
        return this;
    }
    
    
    /// <summary>
    /// Configures the module to use the distributed event bus and cancellation manager providers.
    /// </summary>
    /// <returns></returns>
    public ModuleJobSchedulerGuide UseDistributeProvider()
    {
        PostConfigureServices(_ => { }, key: ConfigProvider);
        DependsOnModule<ModuleEventBusGuide>().Register()
            .AddKeyedCommonEventBus(nameof(ModuleJobScheduler), useDistributed: true);
        DependsOnModule<ModuleCancellationManagerGuide>().Register()
            .AddKeyedCancellationManager(nameof(ModuleJobScheduler), useDistributed: true);
        return this;
    }
   
    /// <summary>
    /// Configures the module to use the in-memory metadata store and event bus and cancellation manager providers.
    /// </summary>
    /// <returns></returns>
    public ModuleJobSchedulerGuide UseInMemoryProvider()
    {
        PostConfigureServices(_ => { }, key: ConfigProvider);
        DependsOnModule<ModuleEventBusGuide>().Register()
            .AddKeyedCommonEventBus(nameof(ModuleJobScheduler), useDistributed: false);
        DependsOnModule<ModuleCancellationManagerGuide>().Register()
            .AddKeyedCancellationManager(nameof(ModuleJobScheduler), useDistributed: false);
        UseInMemoryMetadataStore();
        return this;
    }
}
