using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Modules;

namespace MoLibrary.BackgroundJob.Modules;

public class ModuleBackgroundJobGuide : MoModuleGuide<ModuleBackgroundJob, ModuleBackgroundJobOption, ModuleBackgroundJobGuide>
{
    /// <summary>
    /// 开启作业执行时间监控
    /// </summary>
    public ModuleBackgroundJobGuide EnableWorkerDurationMonitor()
    {
        ConfigureModuleOption(o =>
        {
            o.EnableWorkerDurationMonitor = true;
        });
        DependsOnModule<ModuleTimekeeperGuide>().Register();
        return this;
    }
}