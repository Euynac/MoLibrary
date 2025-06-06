using Microsoft.AspNetCore.Builder;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Dapr.StateStore;
using System.ComponentModel.DataAnnotations;
using MoLibrary.StateStore.Modules;

namespace MoLibrary.Dapr.Modules;


public static class ModuleDaprStateStoreBuilderExtensions
{
    public static ModuleDaprStateStoreGuide ConfigModuleDaprStateStore(this WebApplicationBuilder builder,
        Action<ModuleDaprStateStoreOption>? action = null)
    {
        return new ModuleDaprStateStoreGuide().Register(action);
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
        DependsOnModule<ModuleStateStoreGuide>().Register().RegisterDistributedStateStoreProvider<DaprStateStore>();
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
}

