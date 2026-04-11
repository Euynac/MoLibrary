using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Monica.DependencyInjection.Services.Support;

/// <summary>
/// Warms the diagnostics snapshot after the host starts building application services.
/// </summary>
internal sealed class DependencyInjectionDiagnosticsHostedService(
    DependencyInjectionDiagnosticsRegistry registry,
    ILogger<DependencyInjectionDiagnosticsHostedService> logger) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ = registry.GetSnapshot();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to warm the dependency-injection diagnostics snapshot during startup.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
