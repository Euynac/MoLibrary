using System.Text.Json;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Modules;
using Monica.StateStore.StackExchange.Connection;
using StackExchange.Redis;

namespace Monica.Configuration.Redis.Notifications;

/// <summary>
/// Broadcasts configuration change notifications through Redis pub/sub.
/// </summary>
public sealed class RedisConfigurationChangeBroadcaster : IConfigurationChangeBroadcaster, IDisposable
{
    private readonly ModuleConfigurationRedisOption _option;
    private readonly IConnectionMultiplexer _connection;

    /// <summary>
    /// Creates a Redis change broadcaster.
    /// </summary>
    /// <param name="connectionFactory">Redis connection factory.</param>
    /// <param name="options">Redis configuration options.</param>
    public RedisConfigurationChangeBroadcaster(
        IRedisConnectionFactory connectionFactory,
        IOptions<ModuleConfigurationRedisOption> options)
    {
        _option = options.Value;
        _connection = connectionFactory.CreateConnection(_option.Redis);
    }

    /// <inheritdoc />
    public async Task BroadcastAsync(ConfigurationChangeNotification notification, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var subscriber = _connection.GetSubscriber();
        await subscriber.PublishAsync(
            RedisChannel.Literal(_option.NotificationChannel),
            JsonSerializer.Serialize(RedisConfigurationNotificationDto.FromNotification(notification)));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _connection.Dispose();
    }
}

internal sealed record RedisConfigurationNotificationDto
{
    public required string NotificationId { get; init; }

    public required string DefinitionKey { get; init; }

    public string? LogicalPath { get; init; }

    public string? SourceKey { get; init; }

    public long? Version { get; init; }

    public DateTimeOffset ChangedTime { get; init; }

    public static RedisConfigurationNotificationDto FromNotification(ConfigurationChangeNotification notification)
    {
        return new RedisConfigurationNotificationDto
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
