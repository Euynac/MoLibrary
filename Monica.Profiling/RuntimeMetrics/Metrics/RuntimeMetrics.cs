using System.Diagnostics.Metrics;
using Monica.Profiling.RuntimeMetrics.Models;
using Monica.Profiling.RuntimeMetrics.Providers.EventCounters;

namespace Monica.Profiling.RuntimeMetrics.Metrics;

/// <summary>
/// Exposes runtime metrics cached by the RuntimeMetrics collector through standard .NET observable instruments.
/// </summary>
internal sealed class RuntimeMetrics
{
    private const string GENERATION_TAG_NAME = "generation";
    private const string GENERATION_GEN0 = "gen0";
    private const string GENERATION_GEN1 = "gen1";
    private const string GENERATION_GEN2 = "gen2";
    private const string GENERATION_LOH = "loh";
    private const string GENERATION_POH = "poh";

    private readonly RuntimeMetricsCollector _collector;

    /// <summary>
    /// Initializes runtime observable instruments.
    /// </summary>
    public RuntimeMetrics(IMeterFactory meterFactory, RuntimeMetricsCollector collector)
    {
        _collector = collector;
        var meter = meterFactory.Create(RuntimeMetricNames.MeterName);

        meter.CreateObservableGauge(
            RuntimeMetricNames.GcHeapSize,
            ObserveGcHeapSize,
            unit: "By",
            description: "Managed GC heap size observed by Monica Profiling.");

        meter.CreateObservableGauge(
            RuntimeMetricNames.AllocationRate,
            ObserveAllocationRate,
            unit: "By/s",
            description: "Managed allocation rate observed by Monica Profiling.");

        meter.CreateObservableGauge(
            RuntimeMetricNames.GcGenerationSize,
            ObserveGcGenerationSizes,
            unit: "By",
            description: "Managed heap generation sizes observed by Monica Profiling.");

        meter.CreateObservableGauge(
            RuntimeMetricNames.TimeInGc,
            ObserveTimeInGc,
            unit: "%",
            description: "Percentage of time spent in GC observed by Monica Profiling.");

        meter.CreateObservableGauge(
            RuntimeMetricNames.GcFragmentation,
            ObserveGcFragmentation,
            unit: "%",
            description: "GC fragmentation percentage observed by Monica Profiling.");

        meter.CreateObservableGauge(
            RuntimeMetricNames.WorkingSet,
            ObserveWorkingSet,
            unit: "By",
            description: "Process working set observed by Monica Profiling.");

        meter.CreateObservableGauge(
            RuntimeMetricNames.CpuUsage,
            ObserveCpuUsage,
            unit: "%",
            description: "Process CPU usage observed by Monica Profiling.");

        meter.CreateObservableGauge(
            RuntimeMetricNames.ThreadPoolThreadCount,
            ObserveThreadPoolThreadCount,
            unit: "threads",
            description: "ThreadPool thread count observed by Monica Profiling.");

        meter.CreateObservableCounter(
            RuntimeMetricNames.GcCollections,
            ObserveGcCollections,
            unit: "collections",
            description: "GC collections observed by Monica Profiling.");
    }

    private Measurement<double>[] ObserveGcHeapSize()
    {
        var point = GetCurrentPoint();
        return point is null ? [] : [new(point.GcHeapSizeBytes)];
    }

    private Measurement<double>[] ObserveAllocationRate()
    {
        var point = GetCurrentPoint();
        return point is null ? [] : [new(point.AllocationRateBytesPerSecond)];
    }

    private Measurement<double>[] ObserveGcGenerationSizes()
    {
        var point = GetCurrentPoint();
        if (point is null)
        {
            return [];
        }

        return
        [
            CreateGenerationMeasurement(point.Gen0SizeBytes, GENERATION_GEN0),
            CreateGenerationMeasurement(point.Gen1SizeBytes, GENERATION_GEN1),
            CreateGenerationMeasurement(point.Gen2SizeBytes, GENERATION_GEN2),
            CreateGenerationMeasurement(point.LohSizeBytes, GENERATION_LOH),
            CreateGenerationMeasurement(point.PohSizeBytes, GENERATION_POH)
        ];
    }

    private Measurement<double>[] ObserveTimeInGc()
    {
        var point = GetCurrentPoint();
        return point is null ? [] : [new(point.TimeInGcPercent)];
    }

    private Measurement<double>[] ObserveGcFragmentation()
    {
        var point = GetCurrentPoint();
        return point is null ? [] : [new(point.GcFragmentationPercent)];
    }

    private Measurement<double>[] ObserveWorkingSet()
    {
        var point = GetCurrentPoint();
        return point is null ? [] : [new(point.WorkingSetBytes)];
    }

    private Measurement<double>[] ObserveCpuUsage()
    {
        var point = GetCurrentPoint();
        return point is null ? [] : [new(point.CpuUsagePercent)];
    }

    private Measurement<int>[] ObserveThreadPoolThreadCount()
    {
        var point = GetCurrentPoint();
        return point is null ? [] : [new(point.ThreadCount)];
    }

    private Measurement<double>[] ObserveGcCollections()
    {
        var point = GetCurrentPoint();
        if (point is null)
        {
            return [];
        }

        return
        [
            CreateGenerationMeasurement(point.Gen0GcCount, GENERATION_GEN0),
            CreateGenerationMeasurement(point.Gen1GcCount, GENERATION_GEN1),
            CreateGenerationMeasurement(point.Gen2GcCount, GENERATION_GEN2)
        ];
    }

    private RuntimeMetricsPoint? GetCurrentPoint()
    {
        return _collector.GetCurrentPoint();
    }

    private static Measurement<double> CreateGenerationMeasurement(double value, string generation)
    {
        return new Measurement<double>(
            value,
            new KeyValuePair<string, object?>(GENERATION_TAG_NAME, generation));
    }
}
