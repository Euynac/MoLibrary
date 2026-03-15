using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Features.MoTimekeeper;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleTimekeeperBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Timekeeper 模块
        /// </summary>
        public static ModuleTimekeeperGuide AddTimekeeper(Action<ModuleTimekeeperOption>? action = null)
        {
            return new ModuleTimekeeperGuide().Register(action);
        }
    }
}

/// <summary>
/// Timekeeper模块
/// </summary>
public class ModuleTimekeeper(ModuleTimekeeperOption option)
    : MoModule<ModuleTimekeeper, ModuleTimekeeperOption, ModuleTimekeeperGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Timekeeper;
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