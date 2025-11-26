using Microsoft.AspNetCore.Builder;

namespace MoLibrary.JobScheduler.UI.Modules;

/// <summary>
/// JobScheduler UI 模块注册扩展方法
/// </summary>
public static class ModuleJobSchedulerUIBuilderExtensions
{
    /// <summary>
    /// 配置 JobScheduler UI 模块
    /// </summary>
    public static ModuleJobSchedulerUIGuide ConfigModuleJobSchedulerUI(
        this WebApplicationBuilder builder,
        Action<ModuleJobSchedulerUIOption>? action = null)
    {
        return new ModuleJobSchedulerUIGuide().Register(action);
    }
}
