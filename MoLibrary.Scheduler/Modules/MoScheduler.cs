using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;

namespace MoLibrary.Scheduler.Modules;

/// <summary>
/// MoScheduler模块构建器扩展
/// </summary>
public static class ModuleMoSchedulerBuilderExtensions
{
    public static ModuleMoSchedulerGuide ConfigModuleMoScheduler(this WebApplicationBuilder builder,
        Action<ModuleMoSchedulerOption>? action = null)
    {
        return new ModuleMoSchedulerGuide().Register(action);
    }
}

public class ModuleMoScheduler(ModuleMoSchedulerOption option)
    : MoModuleWithDependencies<ModuleMoScheduler, ModuleMoSchedulerOption, ModuleMoSchedulerGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.MoScheduler;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
    }

    public override void ClaimDependencies()
    {
    }
}

/// <summary>
/// MoScheduler模块向导
/// </summary>
public class ModuleMoSchedulerGuide : MoModuleGuide<ModuleMoScheduler, ModuleMoSchedulerOption, ModuleMoSchedulerGuide>
{
}

/// <summary>
/// MoScheduler模块选项
/// </summary>
public class ModuleMoSchedulerOption : MoModuleOption<ModuleMoScheduler>
{ 

} 