using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Redis.Notifications;

/// <summary>
/// Redis change subscriber placeholder. Phase 5 wires Redis pub/sub transport.
/// </summary>
public sealed class RedisConfigurationChangeSubscriber : IConfigurationChangeSubscriber
{
    /// <inheritdoc />
    public Task SubscribeAsync(Func<ConfigurationChangeNotification, CancellationToken, Task> onNotification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
