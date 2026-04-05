using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Defines the module lifecycle hooks used during registration and initialization.
/// If a module fails during configuration, that module and its dependents stop progressing through the pipeline.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Configures the <see cref="IHostApplicationBuilder"/>.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    void ConfigureBuilder(IHostApplicationBuilder builder);

    /// <summary>
    /// Configures service registrations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Configures services after <see cref="IBusinessTypeIterator"/> has processed business assembly types.
    /// </summary>
    /// <param name="services">The service collection.</param>
    void PostConfigureServices(IServiceCollection services);

}
