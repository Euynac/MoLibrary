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
    /// Gets one history row by identity.
    /// </summary>
    Task<ConfigurationValueHistory?> GetHistoryByIdAsync(string historyId, CancellationToken cancellationToken);

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
    /// Gets one mutation group by identity.
    /// </summary>
    Task<ConfigurationMutationGroup?> GetGroupAsync(string groupId, CancellationToken cancellationToken);
}
