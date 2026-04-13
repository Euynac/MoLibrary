using Microsoft.AspNetCore.Builder;

namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Defines the web-specific lifecycle hooks used by modules that participate in the ASP.NET Core pipeline.
/// Web modules may opt into a degraded non-web mode when they can still provide useful non-web behavior
/// in a generic host.
/// </summary>
public interface IWebModule : IModule
{
    /// <summary>
    /// Gets whether the module can still run its non-web lifecycle phases in a non-ASP.NET Core host.
    /// When this returns <see langword="false"/>, registering the module in a generic host is treated as a startup error.
    /// </summary>
    /// <returns><see langword="true"/> when the module supports degraded non-web execution; otherwise, <see langword="false"/>.</returns>
    bool CanDowngradeToNonWebModule();

    /// <summary>
    /// Configures the application pipeline before and after <c>UseRouting</c>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    void ConfigureApplicationBuilder(IApplicationBuilder app);

    /// <summary>
    /// Configures endpoint mappings for the module.
    /// </summary>
    /// <param name="app">The application builder.</param>
    void ConfigureEndpoints(IApplicationBuilder app);
}
