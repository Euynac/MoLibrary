using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

/// <summary>
/// Default application-level history service that aggregates provider history readers.
/// </summary>
/// <remarks>
/// History sources remain provider-specific so non-audited value sources do not need to fake
/// history support. This service is the single caller-facing entry point that merges those sources
/// into one chronologically ordered result.
/// </remarks>
internal sealed class ConfigurationHistoryService(IEnumerable<IConfigurationHistorySource> historySources) : IConfigurationHistoryService
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
        var histories = new List<ConfigurationValueHistory>();
        foreach (var source in historySources)
        {
            histories.AddRange(await source.QueryHistoryAsync(from, to, definitionKey, logicalPath, mutationGroupId, cancellationToken));
        }

        return Sort(histories);
    }

    /// <inheritdoc />
    public async Task<ConfigurationValueHistory?> GetHistoryByIdAsync(string historyId, CancellationToken cancellationToken)
    {
        foreach (var source in historySources)
        {
            var history = await source.GetHistoryByIdAsync(historyId, cancellationToken);
            if (history is not null)
            {
                return history;
            }
        }

        return null;
    }

    private static IReadOnlyList<ConfigurationValueHistory> Sort(IEnumerable<ConfigurationValueHistory> histories)
    {
        return histories
            .OrderByDescending(history => history.ModifiedTime)
            .ThenByDescending(history => history.Version)
            .ToArray();
    }
}
