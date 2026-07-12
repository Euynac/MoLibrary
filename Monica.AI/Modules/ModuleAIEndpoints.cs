using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Monica.AI.Abstractions;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Extension methods for registering the optional AI HTTP endpoints module.
/// </summary>
public static class ModuleAIEndpointsBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers stateless HTTP endpoints for AI provider discovery.
        /// </summary>
        /// <param name="action">Optional endpoint configuration.</param>
        /// <returns>The AI endpoints module guide.</returns>
        public static ModuleAIEndpointsGuide AddAIEndpoints(Action<ModuleAIEndpointsOption>? action = null)
            => new ModuleAIEndpointsGuide().Register(action);
    }
}

/// <summary>
/// Hosts the optional stateless HTTP surface for Monica AI.
/// </summary>
[ModuleKey(BuiltInModuleKey.AIEndpoints)]
public sealed class ModuleAIEndpoints(ModuleAIEndpointsOption option)
    : WebModuleBase<ModuleAIEndpoints, ModuleAIEndpointsOption, ModuleAIEndpointsGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleAIGuide>().Register();
    }

    /// <inheritdoc />
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            endpoints.MapGet($"{Option.RoutePrefix}/providers", (IAIProviderFactory providers) =>
                    TypedResults.Ok(providers.GetAllProviderInfos()))
                .WithMonicaEndpoint();
        });
    }
}

/// <summary>
/// Configuration for Monica AI HTTP endpoints.
/// </summary>
public sealed class ModuleAIEndpointsOption : ModuleOptions<ModuleAIEndpoints>
{
    private string _routePrefix = "/ai";

    /// <summary>
    /// Gets or sets the route prefix used by all AI endpoints. The default is <c>/ai</c>.
    /// </summary>
    public string RoutePrefix
    {
        get => _routePrefix;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _routePrefix = "/" + value.Trim().Trim('/');
        }
    }
}

/// <summary>
/// Configuration guide for Monica AI HTTP endpoints.
/// </summary>
public sealed class ModuleAIEndpointsGuide
    : WebModuleGuide<ModuleAIEndpoints, ModuleAIEndpointsOption, ModuleAIEndpointsGuide>
{
    /// <summary>
    /// Configures the common AI endpoint route prefix.
    /// </summary>
    /// <param name="routePrefix">Route prefix such as <c>/ai</c>.</param>
    /// <returns>The current guide.</returns>
    public ModuleAIEndpointsGuide MapAIEndpoints(string routePrefix = "/ai")
    {
        ConfigureModuleOption(option => option.RoutePrefix = routePrefix);
        return this;
    }
}
