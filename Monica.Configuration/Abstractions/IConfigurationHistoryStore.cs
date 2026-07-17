using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Persists configuration mutation history and mutation groups.
/// </summary>
public interface IConfigurationHistoryStore
{
    /// <summary>
    /// Gets the store descriptor.
    /// </summary>
    ConfigurationStoreDescriptor Descriptor { get; }

    /// <summary>
    /// Appends one mutation history row.
    /// </summary>
    /// <param name="history">The history row to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AppendHistoryAsync(ConfigurationValueHistory history, CancellationToken cancellationToken);

    /// <summary>
    /// Queries mutation history.
    /// </summary>
    Task<IReadOnlyList<ConfigurationValueHistory>> QueryHistoryAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        LogicalPath? logicalPath,
        string? mutationGroupId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Queries a bounded page of mutation history without splitting matching mutation groups across pages.
    /// </summary>
    /// <param name="request">The filters and mutation-unit pagination bounds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching history page in deterministic newest-first order.</returns>
    Task<ConfigurationHistoryPageResult> QueryHistoryPageAsync(
        ConfigurationHistoryPageRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets one history row by identity.
    /// </summary>
    Task<ConfigurationValueHistory?> GetHistoryByIdAsync(string historyId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets history rows by identity in one logical store operation.
    /// </summary>
    /// <param name="historyIds">The distinct history identities to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rows that exist. Missing identities are omitted and result ordering is unspecified.</returns>
    /// <remarks>
    /// Store implementations should override this fallback with one indexed query or one file scan.
    /// </remarks>
    async Task<IReadOnlyList<ConfigurationValueHistory>> GetHistoriesByIdsAsync(
        IReadOnlyCollection<string> historyIds,
        CancellationToken cancellationToken)
    {
        var histories = new List<ConfigurationValueHistory>(historyIds.Count);
        foreach (var historyId in historyIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var history = await GetHistoryByIdAsync(historyId, cancellationToken);
            if (history is not null)
            {
                histories.Add(history);
            }
        }

        return histories;
    }

    /// <summary>
    /// Creates or updates one mutation group.
    /// </summary>
    Task UpsertGroupAsync(ConfigurationMutationGroup group, CancellationToken cancellationToken);

    /// <summary>
    /// Lists mutation groups.
    /// </summary>
    Task<IReadOnlyList<ConfigurationMutationGroup>> ListGroupsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Queries a bounded page of persisted mutation groups.
    /// </summary>
    /// <param name="request">The filters and pagination bounds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching group page in deterministic newest-first order.</returns>
    Task<ConfigurationMutationGroupPageResult> QueryGroupsPageAsync(
        ConfigurationMutationGroupPageRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets one mutation group by identity.
    /// </summary>
    Task<ConfigurationMutationGroup?> GetGroupAsync(string groupId, CancellationToken cancellationToken);
}
