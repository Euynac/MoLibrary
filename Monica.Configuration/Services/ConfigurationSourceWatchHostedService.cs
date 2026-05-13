using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;

namespace Monica.Configuration.Services;

/// <summary>
/// Subscribes to distributed configuration notifications and reloads the local projection.
/// </summary>
internal sealed class ConfigurationSourceWatchHostedService(
    IEnumerable<IConfigurationChangeSubscriber> subscribers,
    IConfigurationReloadCoordinator reloadCoordinator,
    ILogger<ConfigurationSourceWatchHostedService> logger)
    : BackgroundService
{
    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = subscribers
            .Select(subscriber => RunSubscriberAsync(subscriber, stoppingToken))
            .ToArray();

        return tasks.Length == 0 ? Task.CompletedTask : Task.WhenAll(tasks);
    }

    private async Task RunSubscriberAsync(IConfigurationChangeSubscriber subscriber, CancellationToken stoppingToken)
    {
        try
        {
            await subscriber.SubscribeAsync(async (notification, cancellationToken) =>
            {
                logger.LogDebug(
                    "Received configuration change notification {NotificationId} for definition {DefinitionKey}.",
                    notification.NotificationId,
                    notification.DefinitionKey);

                await reloadCoordinator.ReloadAsync(cancellationToken);
            }, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Configuration change subscriber {SubscriberType} stopped unexpectedly.", subscriber.GetType().FullName);
            throw;
        }
    }
}
