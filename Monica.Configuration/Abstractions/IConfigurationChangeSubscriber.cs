using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Subscribes to distributed configuration change notifications.
/// </summary>
public interface IConfigurationChangeSubscriber
{
    /// <summary>
    /// Subscribes to notifications until the token is cancelled.
    /// </summary>
    /// <param name="onNotification">Notification callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SubscribeAsync(Func<ConfigurationChangeNotification, CancellationToken, Task> onNotification, CancellationToken cancellationToken);
}
