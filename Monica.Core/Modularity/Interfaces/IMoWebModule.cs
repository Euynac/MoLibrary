using Microsoft.AspNetCore.Builder;

namespace Monica.Core.Modularity.Interfaces;

/// <summary>
/// Defines the web-specific lifecycle hooks used by modules that participate in the ASP.NET Core pipeline.
/// </summary>
public interface IMoWebModule : IMoModule
{
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
