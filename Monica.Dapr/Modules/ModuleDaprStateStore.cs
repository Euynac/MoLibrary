using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Dapr.Services;
using Monica.StateStore.Abstractions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDaprStateStoreBuilderExtensions
{
    public static ModuleDaprStateStoreGuide UseDaprStateStoreProvider(this ModuleStateStoreGuide guide,
        Action<ModuleDaprStateStoreOption>? action = null)
    {
        guide.SetCommonDistributedStateStoreProvider<DaprStateStoreProvider>();
        return new ModuleDaprStateStoreGuide().Register(action);
    }
    
    /// <summary>
    /// Registers Dapr state store as a keyed state store provider.
    /// </summary>
    /// <param name="guide">The StateStore module guide.</param>
    /// <param name="serviceKey">The service key that identifies this state store instance.</param>
    /// <param name="configureOptions">Delegate that configures the Dapr state store.</param>
    /// <returns>The same StateStore module guide to support chaining.</returns>
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

            // Register keyed DaprStateStoreProvider
            services.AddKeyedSingleton<IStateStore>(serviceKey, (sp, _) =>
            {
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<ModuleDaprStateStoreOption>>();
                var keyedOptions = Options.Create(optionsMonitor.Get(serviceKey));
                return ActivatorUtilities.CreateInstance<DaprStateStoreProvider>(sp, keyedOptions);
            });
        }, serviceKey);

        guide.RecordKeyedServiceKey(serviceKey);
        return guide;
    }
}

[ModuleKey(BuiltInModuleKey.DaprStateStore)]
public class ModuleDaprStateStore(ModuleDaprStateStoreOption option)
    : ModuleBase<ModuleDaprStateStore, ModuleDaprStateStoreOption, ModuleDaprStateStoreGuide>(option),
      IStateStoreModuleProvider
{

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleDaprClientGuide>().Register();
        DependsOnModule<ModuleStateStoreGuide>().Register();
    }

    #region IStateStoreModuleProvider Implementation

    public ModuleKey ProvidesFor => BuiltInModuleKey.StateStore;

    public EStateStoreProviderType ProviderType => EStateStoreProviderType.Dapr;

    public EStateStoreCapabilities Capabilities =>
        EStateStoreCapabilities.RawStringRetrieval |
        EStateStoreCapabilities.QueryState |
        EStateStoreCapabilities.BulkOperations;

    public string DisplayName => "Dapr";

    #endregion
}

public class
    ModuleDaprStateStoreGuide : ModuleGuide<ModuleDaprStateStore, ModuleDaprStateStoreOption,
    ModuleDaprStateStoreGuide>
{

}

public class ModuleDaprStateStoreOption : ModuleOptions<ModuleDaprStateStore>
{
    /// <summary>
    /// Name of the Dapr state store. Must match the <c>name</c> field in the Dapr state store
    /// component YAML metadata.
    /// </summary>
    [Required]
    public string StateStoreName { get; set; } = null!;

    /// <summary>
    /// Number of concurrent get operations issued by the Dapr runtime to the state store. Values
    /// less than or equal to 0 remove the limit.
    /// </summary>
    public int? DefaultBulkParallelism { get; set; } 
}
