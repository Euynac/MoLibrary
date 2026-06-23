using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Receives distributed configuration reload signals and applies the locally relevant reload work.
/// </summary>
/// <remarks>
/// Implementations treat notifications as invalidation hints. They must reload authoritative local
/// configuration data instead of trusting values from the notification payload.
/// </remarks>
public interface IConfigurationReloadSignalReceiver
{
    /// <summary>
    /// Receives one reload notification.
    /// </summary>
    /// <param name="notification">The reload notification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReceiveAsync(ConfigurationChangeNotification notification, CancellationToken cancellationToken);
}
