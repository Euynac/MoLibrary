using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Features.MoTimekeeper;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Modules;

/// <summary>
/// Timekeeper模块构建器扩展
/// </summary>
public static class ModuleTimekeeperBuilderExtensions
{
    public static ModuleTimekeeperGuide ConfigModuleTimekeeper(this WebApplicationBuilder builder,
        Action<ModuleTimekeeperOption>? action = null)
    {
        return new ModuleTimekeeperGuide().Register(action);
    }
}

/// <summary>
/// Timekeeper模块
/// </summary>
public class ModuleTimekeeper(ModuleTimekeeperOption option)
    : MoModule<ModuleTimekeeper, ModuleTimekeeperOption, ModuleTimekeeperGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Timekeeper;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMoTimekeeperFactory, MoTimekeeperFactory>();
    }
}

/// <summary>
/// Timekeeper模块向导
/// </summary>
public class ModuleTimekeeperGuide : MoModuleGuide<ModuleTimekeeper, ModuleTimekeeperOption, ModuleTimekeeperGuide>
{
}

/// <summary>
/// Timekeeper模块选项
/// </summary>
public class ModuleTimekeeperOption : MoModuleOption<ModuleTimekeeper>
{
}