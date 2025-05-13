using Microsoft.AspNetCore.Builder;
using MoLibrary.Core.Module.TypeFinder;
using System;

namespace MoLibrary.Core.Module.BuilderWrapper;

/// <summary>
/// Provides extension methods for WebApplicationBuilder with Mo module integration.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Event triggered before the WebApplicationBuilder builds the application.
    /// </summary>
    public static event Action<WebApplicationBuilder, Action<ModuleCoreOptionTypeFinder>?>? BeforeBuild;

    /// <summary>
    /// Event triggered after the WebApplicationBuilder builds the application.
    /// </summary>
    public static event Action<WebApplication>? AfterBuild;

    /// <summary>
    /// Builds the WebApplication with Mo module integration by triggering the BeforeBuild and AfterBuild events.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance.</param>
    /// <param name="typeFinderConfigure">Optional configuration for the module type finder.</param>
    /// <returns>The built WebApplication.</returns>
    public static WebApplication MoBuild(this WebApplicationBuilder builder, Action<ModuleCoreOptionTypeFinder>? typeFinderConfigure = null)
    {
        // Trigger BeforeBuild event
        BeforeBuild?.Invoke(builder, typeFinderConfigure);

        var app = builder.Build();

        // Trigger AfterBuild event
        AfterBuild?.Invoke(app);

        return app;
    }
}