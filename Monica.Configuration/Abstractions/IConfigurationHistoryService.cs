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

    /// <summary>
    /// Gets the combined mutation history using optional filters.
    /// </summary>
    /// <param name="from">Earliest modification time to include.</param>
    /// <param name="to">Latest modification time to include.</param>
    /// <param name="definitionKey">Definition key filter.</param>
    /// <param name="logicalPath">Logical path filter.</param>
    /// <param name="mutationGroupId">Mutation group filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching history records from all history-capable sources.</returns>
    Task<IReadOnlyList<ConfigurationValueHistory>> QueryHistoryAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        LogicalPath? logicalPath,
        string? mutationGroupId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets one mutation history row by identity.
    /// </summary>
    /// <param name="historyId">The history record identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The history row, or null when not found.</returns>
    Task<ConfigurationValueHistory?> GetHistoryByIdAsync(string historyId, CancellationToken cancellationToken);
}
