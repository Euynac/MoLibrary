using Microsoft.Extensions.Hosting;

namespace Monica.Configuration.Services;

/// <summary>
/// Reserved hosted service for source-native watches. Phase 4 adds real watch integration.
/// </summary>
internal sealed class ConfigurationSourceWatchHostedService : BackgroundService
{
    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
