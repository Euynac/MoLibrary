using System.Runtime.CompilerServices;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;

namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Base guide for modules that participate in ASP.NET Core middleware and endpoint phases.
/// </summary>
public class WebModuleGuide<TModule, TModuleOption, TModuleGuideSelf>
    : ModuleGuide<TModule, TModuleOption, TModuleGuideSelf>
    where TModuleOption : ModuleOptions<TModule>, new()
    where TModuleGuideSelf : WebModuleGuide<TModule, TModuleOption, TModuleGuideSelf>, new()
    where TModule : WebModuleBase<TModule, TModuleOption, TModuleGuideSelf>
{
    /// <summary>
    /// Configures middleware in the application builder pipeline.
    /// </summary>
    /// <param name="context">The application builder configuration context action.</param>
    /// <param name="order">The concrete middleware execution order.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    protected internal void ConfigureApplicationBuilder(
        Action<ModuleApplicationConfigurationContext<TModuleOption>> context,
        int order,
        string? secondKey = null,
        [CallerMemberName] string key = "")
    {
        ConfigureModule(key, secondKey, registerContext =>
        {
            context.Invoke(new ModuleApplicationConfigurationContext<TModuleOption>(registerContext));
        }, order, ModulePhase.ConfigureApplicationBuilder);
    }

    /// <summary>
    /// <inheritdoc cref="ConfigureApplicationBuilder(System.Action{ModuleApplicationConfigurationContext{TModuleOption}},int,string?,string)"/>
    /// </summary>
    /// <param name="context">The application builder configuration context action.</param>
    /// <param name="order">The middleware execution order enum value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    protected internal void ConfigureApplicationBuilder(
        Action<ModuleApplicationConfigurationContext<TModuleOption>> context,
        ModuleApplicationMiddlewareOrder order,
        string? secondKey = null,
        [CallerMemberName] string key = "")
    {
        ConfigureApplicationBuilder(context, (int)order, secondKey, key);
    }

    /// <summary>
    /// Configures endpoint routing for the module.
    /// </summary>
    /// <param name="context">The endpoint configuration context action.</param>
    /// <param name="order">The concrete execution order value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    protected internal void ConfigureEndpoints(
        Action<ModuleApplicationConfigurationContext<TModuleOption>> context,
        int order,
        string? secondKey = null,
        [CallerMemberName] string key = "")
    {
        ConfigureModule(key, secondKey, registerContext =>
        {
            context.Invoke(new ModuleApplicationConfigurationContext<TModuleOption>(registerContext));
        }, order, ModulePhase.ConfigureEndpoints);
    }

    /// <summary>
    /// <inheritdoc cref="ConfigureEndpoints(System.Action{ModuleApplicationConfigurationContext{TModuleOption}},int,string?,string)"/>
    /// </summary>
    /// <param name="context">The endpoint configuration context action.</param>
    /// <param name="order">The execution order enum value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    protected internal void ConfigureEndpoints(
        Action<ModuleApplicationConfigurationContext<TModuleOption>> context,
        ModuleRegistrationOrder order = ModuleRegistrationOrder.Normal,
        string? secondKey = null,
        [CallerMemberName] string key = "")
    {
        ConfigureEndpoints(context, (int)order, secondKey, key);
    }
}
