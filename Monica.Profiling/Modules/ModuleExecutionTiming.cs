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
using Monica.Profiling.ExecutionTiming.Abstractions;
using Monica.Profiling.ExecutionTiming.Abstractions.Internal;
using Monica.Profiling.ExecutionTiming.Facades;
using Monica.Profiling.ExecutionTiming.Models;
using Monica.Profiling.ExecutionTiming.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleExecutionTimingBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the execution-timing diagnostics module.
        /// </summary>
        public ModuleExecutionTimingGuide AddExecutionTiming(Action<ModuleExecutionTimingOption>? action = null)
        {
            return builder.AddModule<ModuleExecutionTiming, ModuleExecutionTimingOption, ModuleExecutionTimingGuide>(action);
        }
    }
}

/// <summary>
/// Execution-timing module.
/// </summary>
[ModuleKey(BuiltInModuleKey.ExecutionTiming)]
public class ModuleExecutionTiming(ModuleExecutionTimingOption option)
    : WebModuleBase<ModuleExecutionTiming, ModuleExecutionTimingOption, ModuleExecutionTimingGuide>(option)
{
    public override void ClaimDependencies()
    {
        if (Option.AggregationMode == ExecutionTimingAggregationMode.BackgroundBatch)
        {
            DependsOnModule<ModuleHostedServiceGuide>().Register();
        }
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ExecutionTimingCollector>();

        if (Option.AggregationMode == ExecutionTimingAggregationMode.BackgroundBatch)
        {
            services.AddSingleton<BackgroundExecutionTimingCoordinator>();
            services.AddSingleton<IExecutionTimingCoordinator>(sp => sp.GetRequiredService<BackgroundExecutionTimingCoordinator>());
            services.AddSingleton<IExecutionTimingQuery>(sp => sp.GetRequiredService<BackgroundExecutionTimingCoordinator>());
            services.AddHostedService(sp => sp.GetRequiredService<BackgroundExecutionTimingCoordinator>());
        }
        else
        {
            services.AddSingleton<InlineExecutionTimingCoordinator>();
            services.AddSingleton<IExecutionTimingCoordinator>(sp => sp.GetRequiredService<InlineExecutionTimingCoordinator>());
            services.AddSingleton<IExecutionTimingQuery>(sp => sp.GetRequiredService<InlineExecutionTimingCoordinator>());
        }

        services.AddSingleton<IExecutionTimingFactory, ExecutionTimingFactory>();
        services.AddSingleton<ExecutionTimingService>();
        services.AddScoped(sp => new ExecutionTimingFacade(
            sp.GetRequiredService<ExecutionTimingService>(),
            sp.GetRequiredService<ILogger<ExecutionTimingFacade>>()));
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        if (!Option.ExposeExecutionTimingEndpoints)
        {
            return;
        }

        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            endpoints.MapGet(
                    "/execution-timing/statistics",
                    ([FromServices] ExecutionTimingFacade facade) => facade.GetStatistics().GetResponse())
                .WithName("GetExecutionTimingStatistics")
                .WithTags(tagName)
                .WithSummary("Gets aggregated execution-timing statistics.")
                .WithDescription("Returns completed execution-timing statistics ordered by average duration.");

            endpoints.MapGet(
                    "/execution-timing/running",
                    ([FromServices] ExecutionTimingFacade facade) => facade.GetRunningOperations().GetResponse())
                .WithName("GetRunningExecutionTimingOperations")
                .WithTags(tagName)
                .WithSummary("Gets currently running execution-timing operations.")
                .WithDescription("Returns currently running execution-timing operations ordered by elapsed duration.");
        });
    }
}

/// <summary>
/// Configuration guide for the execution-timing module.
/// </summary>
public class ModuleExecutionTimingGuide
    : WebModuleGuide<ModuleExecutionTiming, ModuleExecutionTimingOption, ModuleExecutionTimingGuide>
{
    /// <summary>
    /// Aggregates timing samples immediately on the caller thread.
    /// Use this mode when deterministic immediate statistics visibility matters more than write-path cost.
    /// </summary>
    public ModuleExecutionTimingGuide UseInlineAggregation()
    {
        ConfigureModuleOption(option => option.AggregationMode = ExecutionTimingAggregationMode.Inline);
        return this;
    }

    /// <summary>
    /// Aggregates timing samples on a dedicated background service.
    /// This reduces hot-path write overhead and is closer to the historical MoTimekeeper behavior.
    /// </summary>
    /// <param name="flushInterval">
    /// Optional flush interval for draining the background queue into aggregated statistics.
    /// When omitted, the module option value is used.
    /// </param>
    public ModuleExecutionTimingGuide UseBackgroundBatchAggregation(TimeSpan? flushInterval = null)
    {
        ConfigureModuleOption(option =>
        {
            option.AggregationMode = ExecutionTimingAggregationMode.BackgroundBatch;
            if (flushInterval.HasValue)
            {
                option.BackgroundFlushInterval = flushInterval.Value;
            }
        });

        return this;
    }
}

/// <summary>
/// Configuration options for the execution-timing module.
/// </summary>
public class ModuleExecutionTimingOption : MinimalApiModuleOptions<ModuleExecutionTiming>
{
    /// <summary>
    /// Controls whether execution-timing samples are aggregated inline or by a background batching service.
    /// Use <see cref="ExecutionTimingAggregationMode.BackgroundBatch" /> to reduce hot-path overhead when sampling is frequent.
    /// </summary>
    public ExecutionTimingAggregationMode AggregationMode { get; set; } = ExecutionTimingAggregationMode.BackgroundBatch;

    /// <summary>
    /// Controls how often the background aggregation service drains queued timing samples.
    /// This value is used only when <see cref="AggregationMode" /> is <see cref="ExecutionTimingAggregationMode.BackgroundBatch" />.
    /// Set a positive value; invalid values fall back to an internal default.
    /// </summary>
    public TimeSpan BackgroundFlushInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Exposes minimal API endpoints for execution-timing diagnostics.
    /// Disable this when the module should provide only DI services for in-process consumers.
    /// </summary>
    public bool ExposeExecutionTimingEndpoints { get; set; } = true;
}
