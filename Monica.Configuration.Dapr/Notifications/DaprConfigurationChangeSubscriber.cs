using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Dapr.Notifications;

/// <summary>
/// Dapr change subscriber placeholder. Phase 5 wires Dapr pub/sub transport.
/// </summary>
public sealed class DaprConfigurationChangeSubscriber : IConfigurationChangeSubscriber
{
    /// <inheritdoc />
    public Task SubscribeAsync(Func<ConfigurationChangeNotification, CancellationToken, Task> onNotification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
