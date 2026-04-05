using Microsoft.AspNetCore.Builder;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services;

namespace Monica.Core.Modularity.Extensions;

public static class MonicaApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the Monica module middleware pipeline around <c>UseRouting()</c>.
    /// Call this after <c>builder.Build()</c> and before <see cref="MapMonica(IApplicationBuilder)"/>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder instance.</returns>
    public static IApplicationBuilder UseMonica(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ModuleRegistry.ConfigApplicationPipeline(app, ModuleOrder.MIDDLEWARE_USE_ROUTING, afterGivenOrder: false);
        app.UseRouting();
        ModuleRegistry.ConfigApplicationPipeline(app, ModuleOrder.MIDDLEWARE_USE_ROUTING, afterGivenOrder: true);
        return app;
    }

    /// <summary>
    /// Configures Monica module endpoint mappings. Call this after <see cref="UseMonica(IApplicationBuilder)"/>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder instance.</returns>
    public static IApplicationBuilder MapMonica(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ModuleRegistry.ConfigEndpoints(app);
        return app;
    }
}
