using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Diagnostics.Services;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Modules;
using MonicaApplication = global::Monica.Core.MonicaApplication;

namespace Benchmark.Monica.Core;

/// <summary>
/// Measures diagnostics projection and terminal snapshot lookup while the module catalog grows.
/// The requested count includes the module-system diagnostics module.
/// </summary>
[MemoryDiagnoser]
public class SnapshotModuleCardinalityBenchmarks
{
    private SnapshotBenchmarkHarness _harness = null!;
    private ModuleDiagnosticsService _projectionService = null!;

    /// <summary>Gets or sets the total number of modules represented in the snapshot.</summary>
    [Params(25, 100, 500)]
    public int ModuleCount { get; set; }

    /// <summary>Composes a terminal host and warms its stable cached snapshot.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _harness = SnapshotBenchmarkHarness.Create(ModuleCount);
    }

    /// <summary>Creates an uncached projector before each single-invocation measurement iteration.</summary>
    [IterationSetup(Target = nameof(ProjectSnapshot))]
    public void PrepareProjection()
    {
        _projectionService = _harness.CreateProjectionService();
    }

    /// <summary>Projects an immutable snapshot from captured host state.</summary>
    [Benchmark(Baseline = true)]
    public ModuleDiagnosticsSnapshot ProjectSnapshot()
    {
        return _projectionService.GetSnapshot();
    }

    /// <summary>Returns the stable terminal snapshot through the revision cache.</summary>
    [Benchmark]
    public ModuleDiagnosticsSnapshot GetCachedSnapshot()
    {
        return _harness.GetCachedSnapshot();
    }

    /// <summary>Releases the benchmark host.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _harness.Dispose();
    }
}

/// <summary>
/// Measures diagnostics projection and terminal snapshot lookup across normalized trace cardinalities.
/// Synthetic callback spans are added only after real composition has reached its terminal state.
/// </summary>
[MemoryDiagnoser]
public class SnapshotTraceCardinalityBenchmarks
{
    private const int MODULE_COUNT = 10;
    private SnapshotBenchmarkHarness _harness = null!;
    private ModuleDiagnosticsService _projectionService = null!;

    /// <summary>Gets or sets the exact number of normalized source spans projected into the snapshot.</summary>
    [Params(200, 2_000, 20_000)]
    public int TraceSpanCount { get; set; }

    /// <summary>Composes a terminal host, fills its trace to the requested size, and warms its cache.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _harness = SnapshotBenchmarkHarness.Create(MODULE_COUNT, TraceSpanCount);
    }

    /// <summary>Creates an uncached projector before each single-invocation measurement iteration.</summary>
    [IterationSetup(Target = nameof(ProjectSnapshot))]
    public void PrepareProjection()
    {
        _projectionService = _harness.CreateProjectionService();
    }

    /// <summary>Projects an immutable snapshot from captured host state.</summary>
    [Benchmark(Baseline = true)]
    public ModuleDiagnosticsSnapshot ProjectSnapshot()
    {
        return _projectionService.GetSnapshot();
    }

    /// <summary>Returns the stable terminal snapshot through the revision cache.</summary>
    [Benchmark]
    public ModuleDiagnosticsSnapshot GetCachedSnapshot()
    {
        return _harness.GetCachedSnapshot();
    }

    /// <summary>Releases the benchmark host.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _harness.Dispose();
    }
}

internal sealed class SnapshotBenchmarkHarness : IDisposable
{
    private readonly IHost _host;
    private readonly MonicaApplication _application;
    private readonly IOptions<ModuleSystemOption> _options;
    private readonly IHostEnvironment _environment;
    private readonly ModuleDiagnosticsService _cachedService;

    private SnapshotBenchmarkHarness(IHost host, MonicaApplication application)
    {
        _host = host;
        _application = application;
        _options = host.Services.GetRequiredService<IOptions<ModuleSystemOption>>();
        _environment = host.Services.GetRequiredService<IHostEnvironment>();
        _cachedService = host.Services.GetRequiredService<ModuleDiagnosticsService>();
        _ = _cachedService.GetSnapshot();
    }

    internal static SnapshotBenchmarkHarness Create(int moduleCount, int? traceSpanCount = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(moduleCount, 1);

        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(SnapshotBenchmarkHarness).Assembly));
            monica.AddModuleSystem();
            foreach (var register in CompositionBenchmarkCatalog.CreateRegistrations(
                         moduleCount - 1,
                         emptyPlanPercentage: 100))
            {
                register(monica);
            }
        });

        var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();
        if (traceSpanCount is { } requestedSpanCount)
        {
            FillTrace(application, requestedSpanCount);
        }

        return new SnapshotBenchmarkHarness(host, application);
    }

    internal ModuleDiagnosticsService CreateProjectionService()
    {
        return new ModuleDiagnosticsService(_application, _options, _environment);
    }

    internal ModuleDiagnosticsSnapshot GetCachedSnapshot()
    {
        return _cachedService.GetSnapshot();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _host.Dispose();
    }

    private static void FillTrace(MonicaApplication application, int requestedSpanCount)
    {
        var performance = application.Profiling.GetCompositionPerformance();
        var currentSpanCount = performance.SystemPhases.Count
                               + performance.ModulePhaseExecutions.Count
                               + performance.StartupWorkItems.Count
                               + performance.StartupWorkBarriers.Count;
        if (currentSpanCount > requestedSpanCount)
        {
            throw new InvalidOperationException(
                $"The composed benchmark host already contains {currentSpanCount} spans, " +
                $"which exceeds the requested trace cardinality {requestedSpanCount}.");
        }

        var modules = application.Modules.RuntimeSnapshots;
        if (modules.Count == 0)
        {
            throw new InvalidOperationException("The benchmark host produced no runtime modules.");
        }

        for (var index = currentSpanCount; index < requestedSpanCount; index++)
        {
            var module = modules[index % modules.Count];
            application.Profiling.StartModulePhase(
                module.ModuleType,
                module.ModuleKey,
                module.Order,
                ModulePhase.ConfigureServices);
            application.Profiling.StopModulePhase(module.ModuleType, ModulePhase.ConfigureServices);
        }
    }
}
