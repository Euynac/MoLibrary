using System.Text.Json;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Modules;
using Monica.StateStore.StackExchange.Connection;
using StackExchange.Redis;

namespace Monica.Configuration.Redis.Notifications;

/// <summary>
/// Subscribes to configuration change notifications through Redis pub/sub.
/// </summary>
public sealed class RedisConfigurationChangeSubscriber : IConfigurationChangeSubscriber, IDisposable
{
    private readonly ModuleConfigurationRedisOption _option;
    private readonly IConnectionMultiplexer _connection;

    /// <summary>
    /// Creates a Redis change subscriber.
    /// </summary>
    /// <param name="connectionFactory">Redis connection factory.</param>
    /// <param name="options">Redis configuration options.</param>
    public RedisConfigurationChangeSubscriber(
        IRedisConnectionFactory connectionFactory,
        IOptions<ModuleConfigurationRedisOption> options)
    {
        _option = options.Value;
        _connection = connectionFactory.CreateConnection(_option.Redis);
    }

    /// <inheritdoc />
    public async Task SubscribeAsync(
        Func<ConfigurationChangeNotification, CancellationToken, Task> onNotification,
        CancellationToken cancellationToken)
    {
        var subscriber = _connection.GetSubscriber();
        await subscriber.SubscribeAsync(
            RedisChannel.Literal(_option.NotificationChannel),
            async (_, value) =>
            {
                var notification = JsonSerializer.Deserialize<RedisConfigurationNotificationDto>(value.ToString());
                if (notification is not null)
                {
                    await onNotification(ToNotification(notification), cancellationToken);
                }
            });

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await subscriber.UnsubscribeAsync(RedisChannel.Literal(_option.NotificationChannel));
        }
    }

    private static ConfigurationChangeNotification ToNotification(RedisConfigurationNotificationDto dto)
    {
        return new ConfigurationChangeNotification
        {
            NotificationId = dto.NotificationId,
            DefinitionKey = dto.DefinitionKey,
            LogicalPath = dto.LogicalPath is null ? null : LogicalPath.Parse(dto.LogicalPath),
            SourceKey = dto.SourceKey,
            Version = dto.Version,
            ChangedTime = dto.ChangedTime
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _connection.Dispose();
    }
}
