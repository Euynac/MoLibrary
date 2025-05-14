using Microsoft.AspNetCore.Builder;
using MoLibrary.Core.Module.TypeFinder;

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





    /// <summary>
    /// Adds a <see cref="Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware"/> middleware to the specified <see cref="IApplicationBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="Microsoft.AspNetCore.Builder.IApplicationBuilder"/> to add the middleware to.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    /// <remarks>
    /// <para>
    /// A call to <see cref="EndpointRoutingApplicationBuilderExtensions.UseRouting(IApplicationBuilder)"/> must be followed by a call to
    /// <see cref="EndpointRoutingApplicationBuilderExtensions.UseEndpoints(IApplicationBuilder, Action{Microsoft.AspNetCore.Routing.IEndpointRouteBuilder})"/> for the same <see cref="IApplicationBuilder"/>
    /// instance.
    /// </para>
    /// <para>
    /// The <see cref="Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware"/> defines a point in the middleware pipeline where routing decisions are
    /// made, and an <see cref="Microsoft.AspNetCore.Http.Endpoint"/> is associated with the <see cref="Microsoft.AspNetCore.Http.HttpContext"/>. The <see cref="Microsoft.AspNetCore.Routing.EndpointMiddleware"/>
    /// defines a point in the middleware pipeline where the current <see cref="Microsoft.AspNetCore.Http.Endpoint"/> is executed. Middleware between
    /// the <see cref="Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware"/> and <see cref="Microsoft.AspNetCore.Routing.EndpointMiddleware"/> may observe or change the
    /// <see cref="Microsoft.AspNetCore.Http.Endpoint"/> associated with the <see cref="Microsoft.AspNetCore.Http.HttpContext"/>.
    /// </para>
    /// </remarks>
    public static IApplicationBuilder MoUseRouting(this IApplicationBuilder builder)
    {
        builder.UseRouting();

        return builder;
    }

}