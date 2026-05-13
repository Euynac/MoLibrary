using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Broadcasts configuration change notifications to other service instances.
/// </summary>
public interface IConfigurationChangeBroadcaster
{
    /// <summary>
    /// Broadcasts one change notification.
    /// </summary>
    /// <param name="notification">The change notification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastAsync(ConfigurationChangeNotification notification, CancellationToken cancellationToken);
}
