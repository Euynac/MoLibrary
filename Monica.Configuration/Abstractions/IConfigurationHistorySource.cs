using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Provides persisted configuration mutation history from one backing store.
/// </summary>
public interface IConfigurationHistorySource
{
    /// <summary>
    /// Gets mutation history for one logical path.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="logicalPath">The logical path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching history rows ordered newest first.</returns>
    Task<IReadOnlyList<ConfigurationValueHistory>> GetHistoryAsync(string definitionKey, LogicalPath logicalPath, CancellationToken cancellationToken);
}
