using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Dapr.Notifications;

/// <summary>
/// Dapr change broadcaster placeholder. Phase 5 wires Dapr pub/sub transport.
/// </summary>
public sealed class DaprConfigurationChangeBroadcaster : IConfigurationChangeBroadcaster
{
    /// <inheritdoc />
    public Task BroadcastAsync(ConfigurationChangeNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
