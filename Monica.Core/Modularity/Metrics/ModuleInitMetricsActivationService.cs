using Microsoft.Extensions.Hosting;

namespace Monica.Core.Modularity.Metrics;

/// <summary>
/// Activates module initialization observable instruments when the host starts.
/// </summary>
internal sealed class ModuleInitMetricsActivationService(ModuleInitMetrics metrics) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        GC.KeepAlive(metrics);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
