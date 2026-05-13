using Dapr.Client;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Modules;

namespace Monica.Configuration.Dapr.Notifications;

/// <summary>
/// Broadcasts configuration change notifications through Dapr pub/sub.
/// </summary>
public sealed class DaprConfigurationChangeBroadcaster(
    DaprClient daprClient,
    IOptions<ModuleConfigurationDaprOption> options)
    : IConfigurationChangeBroadcaster
{
    private readonly ModuleConfigurationDaprOption _option = options.Value;

    /// <inheritdoc />
    public Task BroadcastAsync(ConfigurationChangeNotification notification, CancellationToken cancellationToken)
    {
        return daprClient.PublishEventAsync(
            _option.PubSubName,
            _option.NotificationTopic,
            DaprConfigurationNotificationDto.FromNotification(notification),
            cancellationToken);
    }
}

internal sealed record DaprConfigurationNotificationDto
{
    public required string NotificationId { get; init; }

    public required string DefinitionKey { get; init; }

    public string? LogicalPath { get; init; }

    public string? SourceKey { get; init; }

    public long? Version { get; init; }

    public DateTimeOffset ChangedTime { get; init; }

    public static DaprConfigurationNotificationDto FromNotification(ConfigurationChangeNotification notification)
    {
        return new DaprConfigurationNotificationDto
        {
            NotificationId = notification.NotificationId,
            DefinitionKey = notification.DefinitionKey,
            LogicalPath = notification.LogicalPath?.ToCanonicalString(),
            SourceKey = notification.SourceKey,
            Version = notification.Version,
            ChangedTime = notification.ChangedTime
        };
    }
}
