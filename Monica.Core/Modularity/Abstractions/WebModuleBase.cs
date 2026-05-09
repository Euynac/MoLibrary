using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Base type for Monica modules that participate in the ASP.NET Core application pipeline.
/// </summary>
public abstract class WebModuleBase : ModuleBase, IWebModule
{
    /// <summary>
    /// Gets whether this module can continue running its non-web phases in a generic host.
    /// </summary>
    /// <returns><see langword="true"/> when the module supports degraded non-web execution; otherwise, <see langword="false"/>.</returns>
    public virtual bool CanDowngradeToNonWebModule()
    {
        return false;
    }

    /// <summary>
    /// Configures the ASP.NET Core application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public virtual void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
    }

    /// <summary>
    /// Configures ASP.NET Core endpoints for the module.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public virtual void ConfigureEndpoints(IApplicationBuilder app)
    {
    }
}

/// <summary>
/// Base type for Monica modules that support both the core module lifecycle and ASP.NET Core pipeline phases.
/// </summary>
public abstract class WebModuleBase<TModuleSelf, TModuleOption, TModuleGuide>(TModuleOption option)
    : ModuleBase<TModuleSelf, TModuleOption, TModuleGuide>(option), IWebModule
    where TModuleOption : ModuleOptions<TModuleSelf>, new()
    where TModuleSelf : WebModuleBase<TModuleSelf, TModuleOption, TModuleGuide>
    where TModuleGuide : WebModuleGuide<TModuleSelf, TModuleOption, TModuleGuide>, new()
{
    /// <summary>
    /// Gets whether this module can continue running its non-web phases in a generic host.
    /// </summary>
    /// <returns><see langword="true"/> when the module supports degraded non-web execution; otherwise, <see langword="false"/>.</returns>
    public virtual bool CanDowngradeToNonWebModule()
    {
        return false;
    }

    /// <summary>
    /// Configures the ASP.NET Core application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public virtual void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
    }

    /// <summary>
    /// Configures ASP.NET Core endpoints for the module.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public virtual void ConfigureEndpoints(IApplicationBuilder app)
    {
    }

    /// <summary>
    /// Gets the registration order for the module-owned application-builder phase.
    /// </summary>
    /// <remarks>
    /// Override this when built-in middleware must execute at a specific point without relying on entry-only guide methods.
    /// </remarks>
    protected virtual int GetConfigureApplicationBuilderOrder()
    {
        return (int)ModuleApplicationMiddlewareOrder.BeforeUseRouting;
    }

    /// <summary>
    /// Gets the registration order for the module-owned endpoint phase.
    /// </summary>
    /// <remarks>
    /// Override this when built-in endpoint mapping must execute at a specific point without relying on entry-only guide methods.
    /// </remarks>
    protected virtual int GetConfigureEndpointsOrder()
    {
        return -1;
    }

    internal override void ConvertToRegisterRequest()
    {
        base.ConvertToRegisterRequest();

        var guide = new TModuleGuide(); // TODO: this path does not currently preserve the original registration source.

        guide.ConfigureApplicationBuilder(context =>
        {
            ConfigureApplicationBuilder(context.ApplicationBuilder);
        }, GetConfigureApplicationBuilderOrder());

        guide.ConfigureEndpoints(context =>
        {
            ConfigureEndpoints(context.ApplicationBuilder);
        }, GetConfigureEndpointsOrder());
    }

    /// <summary>
    /// Uses endpoint routing only when the module's minimal API surface is enabled.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="configure">Endpoint configuration callback.</param>
    protected void UseEndpoints(IApplicationBuilder builder, Action<IEndpointRouteBuilder> configure)
    {
        if (Option is IMinimalApiModuleOptions option && option.GetIsMinimalApiDisabled())
        {
            return;
        }

        builder.UseEndpoints(endpoints =>
        {
            // Stamp Monica-owned Minimal APIs once at the module boundary so Swagger can classify them without assembly scanning.
            var monicaEndpoints = endpoints.MapGroup(string.Empty)
                .WithMonicaEndpoint();
            configure(monicaEndpoints);
        });
    }
}
