using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Diagnostics.Services;

namespace Monica.Core.Modularity.Metrics;

/// <summary>Publishes cached live measurements until the terminal composition histograms are recorded.</summary>
internal sealed class ModuleInitMetricsActivationService(
    ModuleInitMetrics metrics,
    ModuleDiagnosticsService diagnostics) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            var snapshot = diagnostics.GetSnapshot();
            metrics.Observe(snapshot);
            if (snapshot.IsFinal)
            {
                return;
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                return;
            }
        }
    }
}
