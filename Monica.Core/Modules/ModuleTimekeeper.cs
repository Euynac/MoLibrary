using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Features.MoTimekeeper;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleTimekeeperBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Timekeeper module.
        /// </summary>
        public static ModuleTimekeeperGuide AddTimekeeper(Action<ModuleTimekeeperOption>? action = null)
        {
            return new ModuleTimekeeperGuide().Register(action);
        }
    }
}

/// <summary>
/// Timekeeper module.
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
/// Configuration guide for the Timekeeper module.
/// </summary>
public class ModuleTimekeeperGuide : MoModuleGuide<ModuleTimekeeper, ModuleTimekeeperOption, ModuleTimekeeperGuide>
{
}

/// <summary>
/// Configuration options for the Timekeeper module.
/// </summary>
public class ModuleTimekeeperOption : MoModuleOption<ModuleTimekeeper>
{
}
