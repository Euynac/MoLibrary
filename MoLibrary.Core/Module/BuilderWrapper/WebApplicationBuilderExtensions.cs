using Microsoft.AspNetCore.Builder;
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

    /// <summary>
    /// Internal method to trigger BeforeBuild event from Harmony patches.
    /// </summary>
    internal static void TriggerBeforeBuild(WebApplicationBuilder builder) => BeforeBuild?.Invoke(builder);

    /// <summary>
    /// Internal method to trigger AfterBuild event from Harmony patches.
    /// </summary>
    internal static void TriggerAfterBuild(WebApplication app) => AfterBuild?.Invoke(app);

    /// <summary>
    /// Internal method to trigger BeforeUseRouting event from Harmony patches.
    /// </summary>
    internal static void TriggerBeforeUseRouting(IApplicationBuilder app) => BeforeUseRouting?.Invoke(app);

    /// <summary>
    /// Internal method to trigger AfterUseRouting event from Harmony patches.
    /// </summary>
    internal static void TriggerAfterUseRouting(IApplicationBuilder app) => AfterUseRouting?.Invoke(app);

    /// <summary>
    /// Internal method to trigger BeginUseEndpoints event from Harmony patches.
    /// </summary>
    internal static void TriggerBeginUseEndpoints(IApplicationBuilder app) => BeginUseEndpoints?.Invoke(app);

    public static void ConfigMoModule(this WebApplicationBuilder builder, Action<ModuleCoreOption>? moduleCoreOption = null, Action<ModuleCoreOptionTypeFinder>? typeFinderConfigure = null)
    {
        builder.Services.ConfigActionWrapper(moduleCoreOption, out var option);

        // Initialize Harmony patches to intercept native ASP.NET Core methods
        HarmonyPatchManager.EnsurePatched();

        if (option.EnableRegisterInstantly)
        {
            WebApplicationBuilderInstance = builder;
        }
        builder.Services.GetOrCreateMoModuleSystemTypeFinder(typeFinderConfigure);
    }

    /// <summary>
    /// Builds the WebApplication with Mo module integration by triggering the BeforeBuild and AfterBuild events.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance.</param>
    /// <returns>The built WebApplication.</returns>
    [Obsolete("This method is obsolete. Call builder.Build() directly instead. " +
              "The module system now uses Harmony patches to intercept native ASP.NET Core methods automatically. " +
              "This method will be removed in a future version.")]
    public static WebApplication MoBuild(this WebApplicationBuilder builder)
    {
        // Fallback: manually trigger events if Harmony patching failed
        if (!HarmonyPatchManager.IsPatchingSuccessful)
        {
            BeforeBuild?.Invoke(builder);
        }

        var app = builder.Build();

        if (!HarmonyPatchManager.IsPatchingSuccessful)
        {
            AfterBuild?.Invoke(app);
        }

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
    [Obsolete("This method is obsolete. Call app.UseRouting() directly instead. " +
              "The module system now uses Harmony patches to intercept native ASP.NET Core methods automatically. " +
              "This method will be removed in a future version.")]
    public static IApplicationBuilder UseMoRouting(this IApplicationBuilder builder)
    {
        // Fallback: manually trigger events if Harmony patching failed
        if (!HarmonyPatchManager.IsPatchingSuccessful)
        {
            BeforeUseRouting?.Invoke(builder);
        }

        builder.UseRouting();

        if (!HarmonyPatchManager.IsPatchingSuccessful)
        {
            AfterUseRouting?.Invoke(builder);
        }

        return builder;
    }

    [Obsolete("This method is obsolete. Call app.UseEndpoints() directly instead. " +
              "The module system now uses Harmony patches to intercept native ASP.NET Core methods automatically. " +
              "This method will be removed in a future version.")]
    public static IApplicationBuilder UseMoEndpoints(this IApplicationBuilder builder)
    {
        // Fallback: manually trigger events if Harmony patching failed
        if (!HarmonyPatchManager.IsPatchingSuccessful)
        {
            BeginUseEndpoints?.Invoke(builder);
        }
        return builder;
    }
}