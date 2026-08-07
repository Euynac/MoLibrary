using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity.Diagnostics.Services;

namespace Monica.Core.Modularity.Metrics;

/// <summary>Publishes cached live measurements until the terminal composition histograms are recorded.</summary>
internal sealed class ModuleInitMetricsActivationService(
    ModuleInitMetrics metrics,
    ModuleDiagnosticsService diagnostics,
    MonicaApplication application) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                var snapshot = diagnostics.GetSnapshot();
                metrics.Observe(snapshot);
                var startupTiming = application.StartupTiming;
                if (snapshot.IsFinal)
                {
                    if (startupTiming is null || snapshot.Summary.ApplicationStartupDurationMs.HasValue)
                    {
                        return;
                    }

                    if (startupTiming.IsReady)
                    {
                        // Readiness can advance immediately after a source capture. Retry without polling so the next
                        // revision carries the terminal application measurement.
                        continue;
                    }
                }

                stoppingToken.ThrowIfCancellationRequested();
                if (startupTiming is { IsReady: false })
                {
                    // Wake on the exact readiness mutation so a fast start-then-stop host cannot miss the optional
                    // application histogram.
                    await application.Profiling.WaitForApplicationReadyAsync(stoppingToken);
                    continue;
                }

                // Only non-blocking startup work can keep the module snapshot live after readiness or without tracking.
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Generic Host shutdown owns cancellation. The final observation below closes the immediate start/stop race.
        }
        finally
        {
            metrics.Observe(diagnostics.GetSnapshot());
        }
    }
}
