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
    : MoModule<ModuleProgressBar, ModuleProgressBarOption, ModuleProgressBarGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.ProgressBar;
    }
}

public class ModuleProgressBarGuide : MoModuleGuide<ModuleProgressBar, ModuleProgressBarOption, ModuleProgressBarGuide>
{
    /// <summary>
    /// 配置进度条使用内存状态存储
    /// </summary>
    public ModuleProgressBarGuide UseMemoryStateStore()
    {
        DependsOnModule<ModuleStateStoreGuide>().Register().AddKeyedMemoryStateStore(nameof(ModuleProgressBar));
        
        return this;
    }

    /// <summary>
    /// 配置进度条使用分布式状态存储
    /// </summary>
    public ModuleProgressBarGuide UseDistributedStateStore()
    {
        DependsOnModule<ModuleStateStoreGuide>().Register().AddKeyedDistributedStateStore(nameof(ModuleProgressBar));
        
        return this;
    }


}

public class ModuleProgressBarOption : MoModuleOption<ModuleProgressBar>
{
}