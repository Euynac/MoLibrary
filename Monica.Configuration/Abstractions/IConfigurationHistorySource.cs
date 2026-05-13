using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Provides mutation history from one concrete backing store.
/// </summary>
/// <remarks>
/// This is a provider opt-in contract. A type can be an <see cref="IConfigurationValueSource"/>
/// without implementing history when its backing technology has no durable audit trail. Stores that
/// do persist history, such as the EF Core source, implement this interface so
/// <see cref="IConfigurationHistoryService"/> can aggregate their rows.
/// </remarks>
public interface IConfigurationHistorySource
{
    /// <summary>
    /// Gets this source's mutation history for one logical path.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="logicalPath">The logical path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The matching history rows produced by this source. Implementations should return a stable
    /// newest-first ordering when possible; the orchestration service performs the final combined ordering.
    /// </returns>
    Task<IReadOnlyList<ConfigurationValueHistory>> GetHistoryAsync(string definitionKey, LogicalPath logicalPath, CancellationToken cancellationToken);
}
