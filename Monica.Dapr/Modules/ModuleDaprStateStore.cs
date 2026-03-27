using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Dapr.StateStore;
using Monica.StateStore;
using Monica.StateStore.Providers;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDaprStateStoreBuilderExtensions
{
    public static ModuleDaprStateStoreGuide UseDaprStateStoreProvider(this ModuleStateStoreGuide guide,
        Action<ModuleDaprStateStoreOption>? action = null)
    {
        guide.SetCommonDistributedStateStoreProvider<DaprStateStore>();
        return new ModuleDaprStateStoreGuide().Register(action);
    }
    
    /// <summary>
    /// Add Dapr state store as Keyed StateStore provider
    /// </summary>
    /// <param name="guide">StateStore Module Guide</param>
    /// <param name="serviceKey">The service key that identifies this StateStore instance</param>
    /// <param name="configureOptions">Dapr state storage configuration delegate</param>
    /// <returns>StateStore module guide example to support chained calls</returns>
    public static ModuleStateStoreGuide AddKeyedDaprStateStore(
        this ModuleStateStoreGuide guide,
        string serviceKey,
        Action<ModuleDaprStateStoreOption> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configureOptions);
        new ModuleDaprStateStoreGuide().Register();
        guide.ConfigureStateStoreServices(services =>
        {
            // Register keyed options
            services.Configure(serviceKey, configureOptions);

            // Register keyed DaprStateStore
            services.AddKeyedSingleton<IMoStateStore>(serviceKey, (sp, _) =>
            {
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<ModuleDaprStateStoreOption>>();
                var keyedOptions = Options.Create(optionsMonitor.Get(serviceKey));
                return ActivatorUtilities.CreateInstance<DaprStateStore>(sp, keyedOptions);
            });
        }, serviceKey);

        guide.RecordKeyedServiceKey(serviceKey);
        return guide;
    }
}

[ModuleKey(EMoModuleKey.DaprStateStore)]
public class ModuleDaprStateStore(ModuleDaprStateStoreOption option)
    : MoModule<ModuleDaprStateStore, ModuleDaprStateStoreOption, ModuleDaprStateStoreGuide>(option),
      IStateStoreModuleProvider
{

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleDaprClientGuide>().Register();
        DependsOnModule<ModuleStateStoreGuide>().Register();
    }

    #region IStateStoreModuleProvider Implementation

    public ModuleKey ProvidesFor => EMoModuleKey.StateStore;

    public EStateStoreProviderType ProviderType => EStateStoreProviderType.Dapr;

    public EStateStoreCapabilities Capabilities =>
        EStateStoreCapabilities.RawStringRetrieval |
        EStateStoreCapabilities.QueryState |
        EStateStoreCapabilities.BulkOperations;

    public string DisplayName => "Dapr";

    #endregion
}

public class
    ModuleDaprStateStoreGuide : MoModuleGuide<ModuleDaprStateStore, ModuleDaprStateStoreOption,
    ModuleDaprStateStoreGuide>
{

}

public class ModuleDaprStateStoreOption : MoModuleOption<ModuleDaprStateStore>
{
    /// <summary>
    /// Dapr StateStore name. It needs to be consistent with the name definition in the metadata of the Dapr StateStore.yaml file.
    /// </summary>
    [Required]
    public string StateStoreName { get; set; } = null!;

    /// <summary>
    /// The number of concurrent get operations the Dapr runtime will issue to the state store. a value equal to or smaller than 0 means max parallelism.
    /// </summary>
    public int? DefaultBulkParallelism { get; set; } 
}

