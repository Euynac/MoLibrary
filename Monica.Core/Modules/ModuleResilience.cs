using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
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
        public ModuleResilienceGuide AddResilience(Action<ModuleResilienceOption>? action = null)
        {
            return builder.AddModule<ModuleResilience, ModuleResilienceOption, ModuleResilienceGuide>(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.Resilience)]
public class ModuleResilience(ModuleResilienceOption option)
    : ModuleBase<ModuleResilience, ModuleResilienceOption, ModuleResilienceGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register pipelines without context
        foreach (var (name, configure) in Option.PipelineConfigurations)
        {
            services.AddResiliencePipeline(name, configure);
        }

        // Register pipelines with context (IServiceProvider access)
        foreach (var (name, configure) in Option.PipelineConfigurationsWithContext)
        {
            services.AddResiliencePipeline(name, configure);
        }
    }
}

public class ModuleResilienceGuide : ModuleGuide<ModuleResilience, ModuleResilienceOption, ModuleResilienceGuide>
{
    /// <summary>
    /// Configure the default resilience pipeline using Polly's ResiliencePipelineBuilder directly
    /// </summary>
    /// <param name="configure">Pipeline configuration action that receives Polly's ResiliencePipelineBuilder</param>
    public ModuleResilienceGuide ConfigureDefaultPipeline(Action<ResiliencePipelineBuilder> configure)
    {
        return AddResiliencePipeline(ResiliencePipelineNames.Default, configure);
    }

    /// <summary>
    /// Configure the default resilience pipeline with context (IServiceProvider access)
    /// </summary>
    /// <param name="configure">Pipeline configuration action that receives Polly's ResiliencePipelineBuilder and AddResiliencePipelineContext</param>
    public ModuleResilienceGuide ConfigureDefaultPipeline(Action<ResiliencePipelineBuilder, AddResiliencePipelineContext<string>> configure)
    {
        return AddResiliencePipeline(ResiliencePipelineNames.Default, configure);
    }

    /// <summary>
    /// Add a named resilience pipeline using Polly's ResiliencePipelineBuilder directly
    /// </summary>
    /// <param name="name">Pipeline name for injection</param>
    /// <param name="configure">Pipeline configuration action that receives Polly's ResiliencePipelineBuilder</param>
    public ModuleResilienceGuide AddResiliencePipeline(string name, Action<ResiliencePipelineBuilder> configure)
    {
        ConfigureModuleOption(o => o.PipelineConfigurations[name] = configure, secondKey: name);
        return this;
    }

    /// <summary>
    /// Add a named resilience pipeline with context (IServiceProvider access)
    /// </summary>
    /// <param name="name">Pipeline name for injection</param>
    /// <param name="configure">Pipeline configuration action that receives Polly's ResiliencePipelineBuilder and AddResiliencePipelineContext</param>
    public ModuleResilienceGuide AddResiliencePipeline(string name, Action<ResiliencePipelineBuilder, AddResiliencePipelineContext<string>> configure)
    {
        ConfigureModuleOption(o => o.PipelineConfigurationsWithContext[name] = configure, secondKey: name);
        return this;
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
