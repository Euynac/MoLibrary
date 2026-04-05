using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Profiling.RuntimeMetrics.Facades;
using Monica.Profiling.RuntimeMetrics.Providers.EventCounters;
using Monica.Profiling.RuntimeMetrics.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the runtime metrics module.
/// </summary>
public static class ModuleRuntimeMetricsBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the runtime metrics module.
        /// </summary>
        public static ModuleRuntimeMetricsGuide AddRuntimeMetrics(Action<ModuleRuntimeMetricsOption>? action = null)
        {
            return new ModuleRuntimeMetricsGuide().Register(action);
        }
    }
}

/// <summary>
/// Runtime metrics module.
/// </summary>
[ModuleKey(BuiltInModuleKey.RuntimeMetrics)]
public class ModuleRuntimeMetrics(ModuleRuntimeMetricsOption option)
    : ModuleBase<ModuleRuntimeMetrics, ModuleRuntimeMetricsOption, ModuleRuntimeMetricsGuide>(option)
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_ => new RuntimeMetricsCollector(
            maxHistoryPoints: Option.MaxHistoryPoints,
            sampleIntervalMs: Option.SampleIntervalMs));
        services.AddSingleton<RuntimeMetricsService>();
        services.AddScoped(sp => new RuntimeMetricsFacade(
            sp.GetRequiredService<RuntimeMetricsService>(),
            sp.GetRequiredService<ILogger<RuntimeMetricsFacade>>()));
    }

    /// <inheritdoc />
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            endpoints.MapGet(
                    "/runtime-metrics/latest",
                    ([FromServices] RuntimeMetricsFacade facade) => facade.GetLatestPoint().GetResponse())
                .WithName("GetLatestRuntimeMetrics")
                .WithTags(tagName)
                .WithSummary("Gets the latest runtime metrics sample.")
                .WithDescription("Returns the latest collected CPU, memory, GC, and allocation metrics for the current process.");
        });
    }
}

/// <summary>
/// Fluent guide for the runtime metrics module.
/// </summary>
public class ModuleRuntimeMetricsGuide
    : ModuleGuide<ModuleRuntimeMetrics, ModuleRuntimeMetricsOption, ModuleRuntimeMetricsGuide>
{
}

/// <summary>
/// Configuration options for the runtime metrics module.
/// </summary>
public class ModuleRuntimeMetricsOption : MinimalApiModuleOptions<ModuleRuntimeMetrics>
{
    /// <summary>
    /// Maximum number of runtime metric points retained in memory for trend displays.
    /// </summary>
    public int MaxHistoryPoints { get; set; } = 300;

    /// <summary>
    /// Sampling interval, in milliseconds, used by the runtime EventCounter collector.
    /// Smaller values improve freshness but increase collection overhead.
    /// </summary>
    public int SampleIntervalMs { get; set; } = 1000;
}
