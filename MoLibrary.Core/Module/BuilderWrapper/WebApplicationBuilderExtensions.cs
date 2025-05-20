using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using MoLibrary.Core.Extensions;
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
    public static event Action<WebApplicationBuilder>? BeforeBuild;

    /// <summary>
    /// Event triggered after the WebApplicationBuilder builds the application.
    /// </summary>
    public static event Action<WebApplication>? AfterBuild;

    /// <summary>
    /// Event triggered before the UseRouting middleware is applied.
    /// </summary>
    public static event Action<IApplicationBuilder>? BeforeUseRouting;

    /// <summary>
    /// Event triggered after the UseRouting middleware is applied.
    /// </summary>
    public static event Action<IApplicationBuilder>? AfterUseRouting;

    /// <summary>
    /// Event triggered when start using MoModule related endpoints' middleware.
    /// </summary>
    public static event Action<IApplicationBuilder>? BeginUseEndpoints;

    public static WebApplicationBuilder? WebApplicationBuilderInstance;

    public static void ConfigMoModule(this WebApplicationBuilder builder, Action<ModuleCoreOption>? moduleCoreOption = null, Action<ModuleCoreOptionTypeFinder>? typeFinderConfigure = null)
    {
        builder.Services.ConfigActionWrapper(moduleCoreOption, out var option);
        if (option.EnableRegisterInstantly)
        {
            WebApplicationBuilderInstance = builder;
        }
        var typeFinder = builder.Services.GetOrCreateDomainTypeFinder<MoDomainTypeFinder>();
    }

    /// <summary>
    /// Builds the WebApplication with Mo module integration by triggering the BeforeBuild and AfterBuild events.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance.</param>
    /// <returns>The built WebApplication.</returns>
    public static WebApplication MoBuild(this WebApplicationBuilder builder)
    {
        // Trigger BeforeBuild event
        BeforeBuild?.Invoke(builder);

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
    public static IApplicationBuilder UseMoRouting(this IApplicationBuilder builder)
    {
        // Trigger BeforeUseRouting event
        BeforeUseRouting?.Invoke(builder);

        builder.UseRouting();

        // Trigger AfterUseRouting event
        AfterUseRouting?.Invoke(builder);

        return builder;
    }

  
    public static IApplicationBuilder UseMoEndpoints(this IApplicationBuilder builder)
    {
        // Trigger BeginUseEndpoints event
        BeginUseEndpoints?.Invoke(builder);
        return builder;
    }
}