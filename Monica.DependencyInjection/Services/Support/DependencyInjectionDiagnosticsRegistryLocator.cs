using Microsoft.Extensions.DependencyInjection;

namespace Monica.DependencyInjection.Services.Support;

/// <summary>
/// Resolves the diagnostics registry instance from the service collection during registration-time mutations.
/// </summary>
internal static class DependencyInjectionDiagnosticsRegistryLocator
{
    /// <summary>
    /// Gets the diagnostics registry instance from the service collection when it has already been registered.
    /// </summary>
    public static DependencyInjectionDiagnosticsRegistry? GetRegistry(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        for (var index = services.Count - 1; index >= 0; index--)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType == typeof(DependencyInjectionDiagnosticsRegistry) &&
                descriptor.ImplementationInstance is DependencyInjectionDiagnosticsRegistry registry)
            {
                return registry;
            }
        }

        return null;
    }
}
