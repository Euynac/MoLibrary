using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Reports and refreshes Monica's runtime configuration projection.
/// </summary>
public interface IConfigurationRuntimeReloadService
{
    /// <summary>
    /// Gets a source-of-truth comparison between runtime loaded versions and effective store versions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The runtime reload status report.</returns>
    Task<ConfigurationReloadStatusReport> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reloads runtime configuration providers and returns the refreshed status report.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refreshed runtime reload status report.</returns>
    Task<ConfigurationReloadStatusReport> ReloadAsync(CancellationToken cancellationToken);
}
