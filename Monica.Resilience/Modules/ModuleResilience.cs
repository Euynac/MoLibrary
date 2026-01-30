using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Polly;
using Polly.DependencyInjection;

namespace Monica.Resilience.Modules;

public static class ModuleResilienceBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Resilience 模块
        /// </summary>
        public static ModuleResilienceGuide AddResilience(Action<ModuleResilienceOption>? action = null)
        {
            return new ModuleResilienceGuide().Register(action);
        }
    }
}

public class ModuleResilience(ModuleResilienceOption option)
    : MoModule<ModuleResilience, ModuleResilienceOption, ModuleResilienceGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Resilience;
    }

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

public class ModuleResilienceGuide : MoModuleGuide<ModuleResilience, ModuleResilienceOption, ModuleResilienceGuide>
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

public class ModuleResilienceOption : MoModuleOption<ModuleResilience>
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
    /// RegisterCentre heartbeat pipeline name
    /// </summary>
    public const string RegisterCentre = "register-centre";
}
