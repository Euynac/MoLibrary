using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Redis.Notifications;

/// <summary>
/// Redis change broadcaster placeholder. Phase 5 wires Redis pub/sub transport.
/// </summary>
public sealed class RedisConfigurationChangeBroadcaster : IConfigurationChangeBroadcaster
{
    /// <inheritdoc />
    public Task BroadcastAsync(ConfigurationChangeNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
