using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

/// <summary>
/// Default history service that aggregates history-capable sources.
/// </summary>
internal sealed class ConfigurationHistoryService(IEnumerable<IConfigurationHistorySource> historySources) : IConfigurationHistoryService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationValueHistory>> GetHistoryAsync(
        string definitionKey,
        LogicalPath logicalPath,
        CancellationToken cancellationToken)
    {
        var histories = new List<ConfigurationValueHistory>();
        foreach (var source in historySources)
        {
            histories.AddRange(await source.GetHistoryAsync(definitionKey, logicalPath, cancellationToken));
        }

        return histories
            .OrderByDescending(history => history.ModifiedTime)
            .ThenByDescending(history => history.Version)
            .ToArray();
    }
}
