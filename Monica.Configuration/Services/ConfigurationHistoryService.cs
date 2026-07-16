using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

/// <summary>
/// Default application-level history service backed by the selected history store.
/// </summary>
internal sealed class ConfigurationHistoryService(IConfigurationHistoryStore historyStore) : IConfigurationHistoryService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationValueHistory>> GetHistoryAsync(
        string definitionKey,
        LogicalPath logicalPath,
        CancellationToken cancellationToken)
    {
        return await QueryHistoryAsync(null, null, definitionKey, logicalPath, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationValueHistory>> QueryHistoryAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        LogicalPath? logicalPath,
        string? mutationGroupId,
        CancellationToken cancellationToken)
    {
        return Sort(await historyStore.QueryHistoryAsync(from, to, definitionKey, logicalPath, mutationGroupId, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<ConfigurationHistoryPageResult> QueryHistoryPageAsync(
        ConfigurationHistoryPageRequest request,
        CancellationToken cancellationToken)
    {
        request.Validate();
        var page = await historyStore.QueryHistoryPageAsync(request, cancellationToken);
        return page with { Items = Sort(page.Items) };
    }

    /// <inheritdoc />
    public async Task<ConfigurationValueHistory?> GetHistoryByIdAsync(string historyId, CancellationToken cancellationToken)
    {
        return await historyStore.GetHistoryByIdAsync(historyId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationValueHistory>> GetHistoriesByIdsAsync(
        IReadOnlyCollection<string> historyIds,
        CancellationToken cancellationToken)
    {
        return await historyStore.GetHistoriesByIdsAsync(historyIds, cancellationToken);
    }

    private static IReadOnlyList<ConfigurationValueHistory> Sort(IEnumerable<ConfigurationValueHistory> histories)
    {
        return histories
            .OrderByDescending(history => history.ModifiedTime)
            .ThenByDescending(history => history.Version)
            .ThenByDescending(history => history.HistoryId, StringComparer.Ordinal)
            .ToArray();
    }
}
