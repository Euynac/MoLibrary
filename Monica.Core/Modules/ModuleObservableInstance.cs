using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Features.ObservableInstance;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;

namespace Monica.Core.Modules;

public static class ModuleObservableInstanceBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 ObservableInstance 模块
        /// </summary>
        public static ModuleObservableInstanceGuide AddObservableInstance(Action<ModuleObservableInstanceOption>? action = null)
        {
            return new ModuleObservableInstanceGuide().Register(action);
        }
    }
}

/// <summary>
/// ObservableInstance module provides universal state and exception tracking for all instances.
/// Replaces ModuleExceptionPool functionality with a unified observable pattern.
/// </summary>
public class ModuleObservableInstance(ModuleObservableInstanceOption option)
    : MoModule<ModuleObservableInstance, ModuleObservableInstanceOption, ModuleObservableInstanceGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.ObservableInstance;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
     
        // Register ObservableInstance manager as singleton
        services.AddSingleton<IObservableInstanceManager, ObservableInstanceManager>();
    }
}

/// <summary>
/// Configuration guide for the ObservableInstance module with fluent API
/// </summary>
public class ModuleObservableInstanceGuide : MoModuleGuide<ModuleObservableInstance, ModuleObservableInstanceOption, ModuleObservableInstanceGuide>
{
    
}

/// <summary>
/// Configuration options for the ObservableInstance module
/// </summary>
public class ModuleObservableInstanceOption : MoModuleOption<ModuleObservableInstance>
{
    /// <summary>
    /// Default maximum history size
    /// </summary>
    public int DefaultMaxHistorySize { get; set; } = 100;
}
