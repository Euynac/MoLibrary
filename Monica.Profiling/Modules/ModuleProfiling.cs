using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Profiling.MemoryDiagnostics.Facades;
using Monica.Profiling.MemoryDiagnostics.Providers.DotNetTools;
using Monica.Profiling.MemoryDiagnostics.Services;
using Monica.Profiling.RuntimeMetrics.Facades;
using Monica.Profiling.RuntimeMetrics.Providers.EventCounters;
using Monica.Profiling.RuntimeMetrics.Services;
using Monica.Profiling.TypeAllocation.Facades;
using Monica.Profiling.TypeAllocation.Models;
using Monica.Profiling.TypeAllocation.Providers.ClrMd;
using Monica.Profiling.TypeAllocation.Providers.TraceEvent;
using Monica.Profiling.TypeAllocation.Services;
using Monica.Profiling.TypeAllocation.Services.Support;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleProfilingBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the Profiling module
        /// </summary>
        public static ModuleProfilingGuide AddProfiling(Action<ModuleProfilingOption>? action = null)
        {
            return new ModuleProfilingGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.Profiling)]
public class ModuleProfiling(ModuleProfilingOption option)
    : MoModule<ModuleProfiling, ModuleProfilingOption, ModuleProfilingGuide>(option)
{

    /// <summary>
    /// Configuration service
    /// </summary>
    /// <param name="services">Service collection</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_ => new RuntimeMetricsCollector(
            maxHistoryPoints: Option.MaxHistoryPoints,
            sampleIntervalMs: Option.SampleIntervalMs));
        services.AddSingleton<RuntimeMetricsService>();
        services.AddScoped(sp => new RuntimeMetricsFacade(
            sp.GetRequiredService<RuntimeMetricsService>(),
            sp.GetRequiredService<ILogger<RuntimeMetricsFacade>>()));

        services.AddSingleton<DotNetGcDumpProvider>();
        services.AddSingleton<MemoryDiagnosticsService>();
        services.AddScoped(sp => new MemoryDiagnosticsFacade(
            sp.GetRequiredService<MemoryDiagnosticsService>(),
            sp.GetRequiredService<ILogger<MemoryDiagnosticsFacade>>()));

        if (Option.EnableTypeAllocationTracking)
        {
            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<TraceEventTypeAllocationProvider>>();
                return new TraceEventTypeAllocationProvider(
                    logger,
                    maxTrackedTypes: Option.MaxTrackedTypes,
                    autoStopAfter: Option.AutoStopAfter);
            });
            services.AddSingleton<ClrMdHeapSnapshotProvider>();
            services.AddSingleton<TypeAllocationTrackingService>();
            services.AddScoped(sp => new TypeAllocationFacade(
                sp.GetRequiredService<TypeAllocationTrackingService>(),
                sp.GetRequiredService<ILogger<TypeAllocationFacade>>()));

            if (Option.AutoStartCollection)
            {
                services.AddHostedService<TypeAllocationAutoStartHostedService>();
            }
        }
    }

    /// <summary>
    /// Configure endpoint
    /// </summary>
    /// <param name="app">application builder</param>
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

            endpoints.MapGet("/profiling/simple",
                ([FromServices] RuntimeMetricsFacade facade) => facade.GetLatestPoint().GetResponse())
            .WithName("获取系统性能信息")
            .WithTags(tagName)
            .WithSummary("获取系统性能信息")
            .WithDescription("获取当前进程的CPU使用率和内存使用情况");
        });
    }
}

public class ModuleProfilingGuide : MoModuleGuide<ModuleProfiling, ModuleProfilingOption, ModuleProfilingGuide>
{
}

public class ModuleProfilingOption : MoModuleOptionWithMinimalApi<ModuleProfiling>
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

    /// <summary>
    /// Enables type allocation tracking capabilities for this module.
    /// Disable this when the application should expose only runtime metrics and memory snapshots.
    /// </summary>
    public bool EnableTypeAllocationTracking { get; set; } = true;

    /// <summary>
    /// Automatically starts type allocation collection when the module initializes.
    /// This is useful for unattended diagnostics, but it can increase startup overhead.
    /// </summary>
    public bool AutoStartCollection { get; set; }

    /// <summary>
    /// Selects the allocation sampling mode used when automatic collection starts.
    /// Set this to <see cref="AllocationSamplingMode.Disabled"/> to keep auto start off even if it is configured.
    /// </summary>
    public AllocationSamplingMode DefaultSamplingMode { get; set; } = AllocationSamplingMode.High;

    /// <summary>
    /// Caps the number of tracked types kept in the live allocation ranking.
    /// Larger values preserve more fidelity at the cost of additional memory.
    /// </summary>
    public int MaxTrackedTypes { get; set; } = 500;

    /// <summary>
    /// Stops automatic allocation collection after the configured duration.
    /// Leave this as <see langword="null"/> to keep collection running until it is stopped manually.
    /// </summary>
    public TimeSpan? AutoStopAfter { get; set; } = TimeSpan.FromMinutes(10);
}
