using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Dapr.StateStore;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.StateStore;
using MoLibrary.StateStore.Modules;

namespace MoLibrary.Dapr.Modules;


public static class ModuleDaprStateStoreBuilderExtensions
{
    public static ModuleDaprStateStoreGuide UseDaprStateStoreProvider(this ModuleStateStoreGuide guide,
        Action<ModuleDaprStateStoreOption>? action = null)
    {
        guide.SetCommonDistributedStateStoreProvider<DaprStateStore>();
        return new ModuleDaprStateStoreGuide().Register(action);
    }
    
    /// <summary>
    /// 使用独立的 Dapr 状态存储配置作为 RegisterCentre 的状态存储提供者
    /// </summary>
    /// <remarks>
    /// 此方法允许 RegisterCentre 使用独立的 Dapr StateStore 配置（如不同的 StateStoreName），
    /// 与全局 IDistributedStateStore 配置分离。适用于需要将 RegisterCentre 数据存储在
    /// 专用 StateStore 中的场景。
    /// </remarks>
    /// <param name="guide">RegisterCentre 模块指南</param>
    /// <param name="configureOptions">Dapr 状态存储配置委托</param>
    /// <returns>模块指南实例以支持链式调用</returns>
    public static ModuleRegisterCentreGuide UseDaprKeyedStateStoreProvider(
        this ModuleRegisterCentreGuide guide,
        Action<ModuleDaprStateStoreOption> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);

        const string serviceKey = nameof(ModuleRegisterCentre);
 
        // Use the public method to configure keyed state store
        return guide.UseDistributedStateStore().UseKeyedStateStore(services =>
        {
            // Register keyed options
            services.Configure(serviceKey, configureOptions);

            // Register keyed DaprStateStore
            services.AddKeyedSingleton<IMoStateStore>(serviceKey, (sp, _) =>
            {
                var optionsSnapshot = sp.GetRequiredService<IOptionsSnapshot<ModuleDaprStateStoreOption>>();
                var keyedOptions = Options.Create(optionsSnapshot.Get(serviceKey));
                return ActivatorUtilities.CreateInstance<DaprStateStore>(sp, keyedOptions);
            });
        });
    }
}

public class ModuleDaprStateStore(ModuleDaprStateStoreOption option)
    : MoModuleWithDependencies<ModuleDaprStateStore, ModuleDaprStateStoreOption, ModuleDaprStateStoreGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DaprStateStore;
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleDaprClientGuide>().Register();
        DependsOnModule<ModuleStateStoreGuide>().Register();
    }
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

