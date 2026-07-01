using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Persists unified configuration version snapshots.
/// </summary>
public interface IConfigurationUnifiedVersionStore
{
    /// <summary>
    /// Appends one unified version snapshot and assigns the next global version number.
    /// </summary>
    /// <param name="request">The snapshot creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The persisted snapshot.</returns>
    Task<ConfigurationUnifiedVersionSnapshot> AppendVersionAsync(
        ConfigurationUnifiedVersionCreateRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists unified version summaries from newest to oldest.
    /// </summary>
    /// <param name="from">Earliest creation time to include.</param>
    /// <param name="to">Latest creation time to include.</param>
    /// <param name="definitionKey">Optional definition key filter.</param>
    /// <param name="limit">Maximum number of versions to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching version summaries.</returns>
    Task<IReadOnlyList<ConfigurationUnifiedVersionSummary>> ListVersionsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets one unified version snapshot.
    /// </summary>
    /// <param name="version">The unified version number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The snapshot, or null when not found.</returns>
    Task<ConfigurationUnifiedVersionSnapshot?> GetVersionAsync(long version, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the snapshot created for a mutation group.
    /// </summary>
    /// <param name="mutationGroupId">The mutation group identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The snapshot, or null when the group has not been captured.</returns>
    Task<ConfigurationUnifiedVersionSnapshot?> GetVersionByMutationGroupAsync(
        string mutationGroupId,
        CancellationToken cancellationToken);
}
