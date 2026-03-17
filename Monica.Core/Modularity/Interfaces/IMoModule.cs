using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Interfaces;

public interface IMoModuleStaticInfo
{
    static abstract ModuleKey GetStaticModuleKey();
}

/// <summary>
/// Defines the module lifecycle hooks used during registration and initialization.
/// If a module fails during configuration, that module and its dependents stop progressing through the pipeline.
/// </summary>
public interface IMoModule
{
    /// <summary>
    /// Configures the <see cref="WebApplicationBuilder"/>.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    void ConfigureBuilder(WebApplicationBuilder builder);

    /// <summary>
    /// Configures service registrations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Configures services after <see cref="IWantIterateBusinessTypes"/> has processed business assembly types.
    /// </summary>
    /// <param name="services">The service collection.</param>
    void PostConfigureServices(IServiceCollection services);

    /// <summary>
    /// Configures the application pipeline before `UseRouting`.
    /// </summary>
    /// <param name="app">The application builder.</param>
    void ConfigureApplicationBuilder(IApplicationBuilder app);

    ModuleKey GetModuleKey();
}
