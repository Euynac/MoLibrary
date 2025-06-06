using Microsoft.AspNetCore.Builder;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.StateStore.Modules;


public static class ModuleProgressBarBuilderExtensions
{
    public static ModuleProgressBarGuide ConfigModuleProgressBar(this WebApplicationBuilder builder,
        Action<ModuleProgressBarOption>? action = null)
    {
        return new ModuleProgressBarGuide().Register(action);
    }
}

public class ModuleProgressBar(ModuleProgressBarOption option)
    : MoModuleWithDependencies<ModuleProgressBar, ModuleProgressBarOption, ModuleProgressBarGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.ProgressBar;
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleCancellationManagerGuide>().Register().AddKeyedCancellationManager(nameof(ModuleProgressBar), Option.UseDistributedStateStore);
    }
}

public class ModuleProgressBarGuide : MoModuleGuide<ModuleProgressBar, ModuleProgressBarOption, ModuleProgressBarGuide>
{
    
}

public class ModuleProgressBarOption : MoModuleOption<ModuleProgressBar>
{
    /// <summary>
    /// 使用分布式存储
    /// </summary>
    public bool UseDistributedStateStore { get; set; }
}