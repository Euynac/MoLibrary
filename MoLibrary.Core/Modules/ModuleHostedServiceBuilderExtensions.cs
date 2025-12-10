using Microsoft.AspNetCore.Builder;

namespace MoLibrary.Core.Modules;

/// <summary>
/// Extension methods for configuring the HostedService observability module
/// </summary>
public static class ModuleHostedServiceBuilderExtensions
{
    /// <summary>
    /// Configures the HostedService observability module
    /// </summary>
    /// <param name="builder">The web application builder</param>
    /// <param name="action">Optional configuration action</param>
    /// <returns>The module guide for fluent configuration</returns>
    public static ModuleHostedServiceGuide ConfigModuleHostedService(
        this WebApplicationBuilder builder,
        Action<ModuleHostedServiceOption>? action = null)
    {
        return new ModuleHostedServiceGuide().Register(action);
    }
}
