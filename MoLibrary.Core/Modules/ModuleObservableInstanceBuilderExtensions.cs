using Microsoft.AspNetCore.Builder;

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
