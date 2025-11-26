using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.JobScheduler.UI.Modules;

/// <summary>
/// JobScheduler UI 模块配置指南
/// </summary>
public class ModuleJobSchedulerUIGuide
    : MoModuleGuide<ModuleJobSchedulerUI, ModuleJobSchedulerUIOption, ModuleJobSchedulerUIGuide>
{
    // 配置方法可在后续需要时添加
    // 目前通过 ConfigModuleJobSchedulerUI(options => { ... }) 直接配置即可
}
