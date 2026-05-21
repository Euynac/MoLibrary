using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Notifies other processes that a managed configuration document changed.
/// </summary>
/// <remarks>
/// The core module only defines this abstraction in v1. Distributed hot reload requires a concrete
/// provider implementation to be registered by an application or storage module.
/// </remarks>
public interface IConfigurationChangeNotifier
{
    /// <summary>
    /// Sends a change notification.
    /// </summary>
    /// <param name="notification">The change notification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyAsync(ConfigurationChangeNotification notification, CancellationToken cancellationToken);
}
