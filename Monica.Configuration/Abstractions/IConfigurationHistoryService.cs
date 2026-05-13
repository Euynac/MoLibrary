using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Provides the application-level history query API used by facades, endpoints, and UI surfaces.
/// </summary>
/// <remarks>
/// This service is an orchestration boundary, not a storage adapter. Implementations may combine
/// zero or more <see cref="IConfigurationHistorySource"/> instances, normalize ordering, and hide
/// provider-specific history details from callers.
/// </remarks>
public interface IConfigurationHistoryService
{
    /// <summary>
    /// Gets the combined mutation history for one logical path.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="logicalPath">The logical path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// History records from all history-capable sources, ordered for caller consumption.
    /// </returns>
    Task<IReadOnlyList<ConfigurationValueHistory>> GetHistoryAsync(string definitionKey, LogicalPath logicalPath, CancellationToken cancellationToken);
}
