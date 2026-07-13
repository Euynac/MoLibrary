using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Notifies other processes that managed configuration projections should reload.
/// </summary>
/// <remarks>
/// The core module only defines this abstraction in v1. Distributed hot reload requires a concrete
/// provider implementation to be registered by an application or storage module.
/// </remarks>
public interface IConfigurationChangeNotifier
{
    /// <summary>
    /// Sends a reload signal.
    /// </summary>
    /// <param name="signal">The reload signal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyAsync(ConfigurationReloadSignal signal, CancellationToken cancellationToken);
}
