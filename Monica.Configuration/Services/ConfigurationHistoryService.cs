using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

/// <summary>
/// Default history service. Provider-backed history is added by persistent sources.
/// </summary>
internal sealed class ConfigurationHistoryService : IConfigurationHistoryService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ConfigurationValueHistory>> GetHistoryAsync(
        string definitionKey,
        LogicalPath logicalPath,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ConfigurationValueHistory>>([]);
    }
}
