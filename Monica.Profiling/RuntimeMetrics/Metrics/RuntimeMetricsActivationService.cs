using Microsoft.Extensions.Hosting;
using Monica.Profiling.RuntimeMetrics.Providers.EventCounters;

namespace Monica.Profiling.RuntimeMetrics.Metrics;

/// <summary>
/// Activates runtime metric collection and observable instruments when the host starts.
/// </summary>
internal sealed class RuntimeMetricsActivationService(
    RuntimeMetricsCollector collector,
    RuntimeMetrics metrics)
    : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        GC.KeepAlive(collector);
        GC.KeepAlive(metrics);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
