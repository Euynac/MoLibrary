using Microsoft.Extensions.DependencyInjection;
using Monica.Core;

using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
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
        public ModuleRegistration<ModuleObservableInstance, ModuleObservableInstanceOption> AddObservableInstance(
            Action<ModuleObservableInstanceOption>? action = null)
        {
            return builder.AddModule<ModuleObservableInstance, ModuleObservableInstanceOption>(action);
        }
    }
}

/// <summary>
/// ObservableInstance module provides universal state and exception tracking for all instances.
/// Replaces ModuleExceptionPool functionality with a unified observable pattern.
/// </summary>
public class ModuleObservableInstance : MonicaModule<ModuleObservableInstanceOption>
{

    public override void ConfigureServices(ModuleContext<ModuleObservableInstanceOption> context)
    {
        context.Services.AddSingleton<ObservableInstanceFacade>();
        context.Services.AddSingleton<IObservableInstanceRegistry, ObservableInstanceRegistry>();
    }
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
