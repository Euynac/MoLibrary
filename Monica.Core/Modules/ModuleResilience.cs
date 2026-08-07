using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Polly;
using Polly.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleResilienceBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers and configures the Resilience module.
        /// </summary>
        public ModuleRegistration<ModuleResilience, ModuleResilienceOption> AddResilience(
            Action<ModuleResilienceOption>? action = null)
        {
            return builder.AddModule<ModuleResilience, ModuleResilienceOption>(action);
        }
    }

    extension(ModuleRegistration<ModuleResilience, ModuleResilienceOption> registration)
    {
        public ModuleRegistration<ModuleResilience, ModuleResilienceOption> ConfigureDefaultPipeline(
            Action<ResiliencePipelineBuilder> configure)
        {
            return registration.AddResiliencePipeline(ResiliencePipelineNames.Default, configure);
        }

        public ModuleRegistration<ModuleResilience, ModuleResilienceOption> ConfigureDefaultPipeline(
            Action<ResiliencePipelineBuilder, AddResiliencePipelineContext<string>> configure)
        {
            return registration.AddResiliencePipeline(ResiliencePipelineNames.Default, configure);
        }

        public ModuleRegistration<ModuleResilience, ModuleResilienceOption> AddResiliencePipeline(
            string name,
            Action<ResiliencePipelineBuilder> configure)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(configure);
            return registration.Configure(options => options.PipelineConfigurations[name] = configure);
        }

        public ModuleRegistration<ModuleResilience, ModuleResilienceOption> AddResiliencePipeline(
            string name,
            Action<ResiliencePipelineBuilder, AddResiliencePipelineContext<string>> configure)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(configure);
            return registration.Configure(options => options.PipelineConfigurationsWithContext[name] = configure);
        }
    }
}

public class ModuleResilience : MonicaModule<ModuleResilienceOption>
{
    public override void ConfigureServices(ModuleContext<ModuleResilienceOption> context)
    {
        foreach (var (name, configure) in Option.PipelineConfigurations)
        {
            context.Services.AddResiliencePipeline(name, configure);
        }

        foreach (var (name, configure) in Option.PipelineConfigurationsWithContext)
        {
            context.Services.AddResiliencePipeline(name, configure);
        }
    }
}

public class ModuleResilienceOption : ModuleOptions<ModuleResilience>
{
    /// <summary>
    /// Pipeline configurations stored as deferred actions (without context)
    /// </summary>
    public Dictionary<string, Action<ResiliencePipelineBuilder>> PipelineConfigurations { get; } = new();

    /// <summary>
    /// Pipeline configurations stored as deferred actions (with context for IServiceProvider access)
    /// </summary>
    public Dictionary<string, Action<ResiliencePipelineBuilder, AddResiliencePipelineContext<string>>> PipelineConfigurationsWithContext { get; } = new();
}

/// <summary>
/// Well-known resilience pipeline names
/// </summary>
public static class ResiliencePipelineNames
{
    /// <summary>
    /// Default pipeline name
    /// </summary>
    public const string Default = "default";

    /// <summary>
    /// Service discovery heartbeat pipeline name
    /// </summary>
    public const string ServiceDiscovery = "service-discovery";
}
