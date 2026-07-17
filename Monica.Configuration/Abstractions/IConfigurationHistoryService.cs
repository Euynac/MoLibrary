using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Provides the application-level history query API used by facades, endpoints, and UI surfaces.
/// </summary>
/// <remarks>
/// This service is an orchestration boundary, not a storage adapter. Implementations use the selected
/// <see cref="IConfigurationHistoryStore"/>, normalize ordering, and hide store-specific history details from callers.
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
    /// History records from the active history store, ordered for caller consumption.
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
    /// <returns>Matching history records from the active history store.</returns>
    Task<IReadOnlyList<ConfigurationValueHistory>> QueryHistoryAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        LogicalPath? logicalPath,
        string? mutationGroupId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a bounded page of mutation history without splitting matching mutation groups across pages.
    /// </summary>
    /// <param name="request">The filters and mutation-unit pagination bounds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching history page in deterministic newest-first order.</returns>
    Task<ConfigurationHistoryPageResult> QueryHistoryPageAsync(
        ConfigurationHistoryPageRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets one mutation history row by identity.
    /// </summary>
    /// <param name="historyId">The history record identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The history row, or null when not found.</returns>
    Task<ConfigurationValueHistory?> GetHistoryByIdAsync(string historyId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets multiple mutation history rows in one logical store operation.
    /// </summary>
    /// <param name="historyIds">The distinct history identities to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rows that exist. Missing identities are omitted and result ordering is unspecified.</returns>
    Task<IReadOnlyList<ConfigurationValueHistory>> GetHistoriesByIdsAsync(
        IReadOnlyCollection<string> historyIds,
        CancellationToken cancellationToken);
}
