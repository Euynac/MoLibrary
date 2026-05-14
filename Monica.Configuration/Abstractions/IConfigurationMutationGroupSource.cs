using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Persists mutation group records for one backing store.
/// </summary>
public interface IConfigurationMutationGroupSource
{
    /// <summary>
    /// Upserts one mutation group.
    /// </summary>
    /// <param name="group">The group to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(ConfigurationMutationGroup group, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one mutation group by identity.
    /// </summary>
    /// <param name="groupId">The group identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The group, or null when not found.</returns>
    Task<ConfigurationMutationGroup?> GetAsync(string groupId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists mutation groups using optional filters.
    /// </summary>
    /// <param name="from">Earliest creation time to include.</param>
    /// <param name="to">Latest creation time to include.</param>
    /// <param name="definitionKey">Definition key filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching groups.</returns>
    Task<IReadOnlyList<ConfigurationMutationGroup>> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        CancellationToken cancellationToken);
}
