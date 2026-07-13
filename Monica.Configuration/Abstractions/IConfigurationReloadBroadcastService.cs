using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Reloads the current Monica projection and broadcasts a best-effort reload-all signal.
/// </summary>
public interface IConfigurationReloadBroadcastService
{
    /// <summary>
    /// Reloads locally and broadcasts to other processes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The local reload and distributed publication outcome.</returns>
    Task<ConfigurationReloadBroadcastResult> BroadcastAllAsync(CancellationToken cancellationToken);
}
