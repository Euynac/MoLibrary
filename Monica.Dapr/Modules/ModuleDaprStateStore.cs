using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
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
    /// 添加 Dapr 状态存储作为 Keyed StateStore 提供者
    /// </summary>
    /// <param name="guide">StateStore 模块指南</param>
    /// <param name="serviceKey">服务键，用于标识此 StateStore 实例</param>
    /// <param name="configureOptions">Dapr 状态存储配置委托</param>
    /// <returns>StateStore 模块指南实例以支持链式调用</returns>
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
            // 注册 keyed options
            services.Configure(serviceKey, configureOptions);

            // 注册 keyed DaprStateStore
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

public class ModuleDaprStateStore(ModuleDaprStateStoreOption option)
    : MoModuleWithDependencies<ModuleDaprStateStore, ModuleDaprStateStoreOption, ModuleDaprStateStoreGuide>(option),
      IStateStoreModuleProvider
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.DaprStateStore;
    }

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
    /// Dapr StateStore名称。需要与Dapr StateStore.yaml文件metadata中的name定义一致
    /// </summary>
    [Required]
    public string StateStoreName { get; set; } = null!;

    /// <summary>
    /// The number of concurrent get operations the Dapr runtime will issue to the state store. a value equal to or smaller than 0 means max parallelism.
    /// </summary>
    public int? DefaultBulkParallelism { get; set; } 
}

