using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Dapr.StateStore;
using MoLibrary.StateStore;
using MoLibrary.Tool.MoResponse;
using System.ComponentModel.DataAnnotations;

namespace MoLibrary.Dapr.Modules;


public static class ModuleDaprStateStoreBuilderExtensions
{
    public static ModuleDaprStateStoreGuide ConfigModuleDaprStateStore(this IServiceCollection services,
        Action<ModuleDaprStateStoreOption>? action = null)
    {
        return new ModuleDaprStateStoreGuide().Register(action);
    }
}

public class ModuleDaprStateStore(ModuleDaprStateStoreOption option)
    : MoModuleWithDependencies<ModuleDaprStateStore, ModuleDaprStateStoreOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DaprStateStore;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IStateStore, DaprStateStore>();
        return Res.Ok();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleDaprClientGuide>().Register();
    }
}

public class
    ModuleDaprStateStoreGuide : MoModuleGuide<ModuleDaprStateStore, ModuleDaprStateStoreOption,
    ModuleDaprStateStoreGuide>
{


}


public class ModuleDaprStateStoreOption : IMoModuleOption<ModuleDaprStateStore>
{
    /// <summary>
    /// Dapr StateStore名称。需要与Dapr StateStore.yaml文件metadata中的name定义一致
    /// </summary>
    [Required]
    public string StateStoreName { get; set; } = null!;
}

