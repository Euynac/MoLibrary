using Microsoft.Extensions.DependencyInjection;
using Monica.Core;

using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Core.ObservableInstance.Facades;
using Monica.Core.ObservableInstance.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleObservableInstanceBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the ObservableInstance module.
        /// </summary>
        public ModuleObservableInstanceGuide AddObservableInstance(Action<ModuleObservableInstanceOption>? action = null)
        {
            return builder.AddModule<ModuleObservableInstance, ModuleObservableInstanceOption, ModuleObservableInstanceGuide>(action);
        }
    }
}

/// <summary>
/// ObservableInstance module provides universal state and exception tracking for all instances.
/// Replaces ModuleExceptionPool functionality with a unified observable pattern.
/// </summary>
[ModuleKey(BuiltInModuleKey.ObservableInstance)]
public class ModuleObservableInstance(ModuleObservableInstanceOption option)
    : ModuleBase<ModuleObservableInstance, ModuleObservableInstanceOption, ModuleObservableInstanceGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ObservableInstanceFacade>();
        services.AddSingleton<IObservableInstanceRegistry, ObservableInstanceRegistry>();
    }
}

/// <summary>
/// Configuration guide for the ObservableInstance module with fluent API
/// </summary>
public class ModuleObservableInstanceGuide : ModuleGuide<ModuleObservableInstance, ModuleObservableInstanceOption, ModuleObservableInstanceGuide>
{
    
}

/// <summary>
/// Configuration options for the ObservableInstance module
/// </summary>
public class ModuleObservableInstanceOption : ModuleOptions<ModuleObservableInstance>
{
    /// <summary>
    /// Default maximum history size
    /// </summary>
    public int DefaultMaxHistorySize { get; set; } = 100;
}
