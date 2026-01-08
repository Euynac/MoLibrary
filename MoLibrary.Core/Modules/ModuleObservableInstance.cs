using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Modules;

/// <summary>
/// Extension methods for configuring the ObservableInstance module
/// </summary>
public static class ModuleObservableInstanceBuilderExtensions
{
    /// <summary>
    /// Configures the ObservableInstance module
    /// </summary>
    /// <param name="builder">The web application builder</param>
    /// <param name="action">Optional configuration action</param>
    /// <returns>The module guide for fluent configuration</returns>
    public static ModuleObservableInstanceGuide ConfigModuleObservableInstance(
        this WebApplicationBuilder builder,
        Action<ModuleObservableInstanceOption>? action = null)
    {
        return new ModuleObservableInstanceGuide().Register(action);
    }
}

/// <summary>
/// ObservableInstance module provides universal state and exception tracking for all instances.
/// Replaces ModuleExceptionPool functionality with a unified observable pattern.
/// </summary>
public class ModuleObservableInstance(ModuleObservableInstanceOption option)
    : MoModule<ModuleObservableInstance, ModuleObservableInstanceOption, ModuleObservableInstanceGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.ObservableInstance;
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
