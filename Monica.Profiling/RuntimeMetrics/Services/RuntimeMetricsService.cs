using Monica.Profiling.RuntimeMetrics.Models;
using Monica.Profiling.RuntimeMetrics.Providers.EventCounters;

namespace Monica.Profiling.RuntimeMetrics.Services;

internal sealed class RuntimeMetricsService(RuntimeMetricsCollector collector)
{
    public RuntimeMetricsPoint GetLatestPoint()
    {
        return collector.GetCurrentPoint()
               ?? throw new InvalidOperationException("Runtime metrics have not been collected yet.");
    }

    public RuntimeMetricsTrend GetTrend()
    {
        return collector.GetTrend();
    }

    public Action SubscribeToUpdates(Action<RuntimeMetricsPoint> callback)
    {
        collector.MetricsUpdated += callback;
        return () => collector.MetricsUpdated -= callback;
    }
}
