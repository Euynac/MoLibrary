using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Coordinates persisted mutation group lifecycle operations.
/// </summary>
public interface IConfigurationMutationGroupService
{
    /// <summary>
    /// Creates and persists a new mutation group before the first mutation is applied.
    /// </summary>
    /// <param name="label">Operator-facing label.</param>
    /// <param name="reason">Optional mutation reason.</param>
    /// <param name="context">Audit context. A non-empty mutation-group identity is preserved for history correlation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created mutation group.</returns>
    Task<ConfigurationMutationGroup> BeginAsync(
        string label,
        string? reason,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks a group as fully applied.
    /// </summary>
    /// <param name="groupId">The group identity.</param>
    /// <param name="mutationCount">The number of applied mutations.</param>
    /// <param name="definitionKeys">The distinct definition keys touched by the group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CompleteAsync(
        string groupId,
        int mutationCount,
        IReadOnlyList<string> definitionKeys,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks a group as partially applied.
    /// </summary>
    /// <param name="groupId">The group identity.</param>
    /// <param name="successfulCount">The number of successful mutations.</param>
    /// <param name="definitionKeys">The distinct definition keys touched by successful mutations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkPartialAsync(
        string groupId,
        int successfulCount,
        IReadOnlyList<string> definitionKeys,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks a group as rolled back by another group.
    /// </summary>
    /// <param name="groupId">The original group identity.</param>
    /// <param name="rollbackGroupId">The rollback group identity.</param>
    /// <param name="rolledBackTime">The rollback completion time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkRolledBackAsync(
        string groupId,
        string rollbackGroupId,
        DateTimeOffset rolledBackTime,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists mutation groups using optional filters.
    /// </summary>
    /// <param name="from">Earliest creation time to include.</param>
    /// <param name="to">Latest creation time to include.</param>
    /// <param name="definitionKey">Definition key filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching mutation groups.</returns>
    Task<IReadOnlyList<ConfigurationMutationGroup>> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a bounded page of persisted mutation groups.
    /// </summary>
    /// <param name="request">The filters and pagination bounds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching group page in deterministic newest-first order.</returns>
    Task<ConfigurationMutationGroupPageResult> QueryPageAsync(
        ConfigurationMutationGroupPageRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets one mutation group by identity.
    /// </summary>
    /// <param name="groupId">The group identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The group, or null when not found.</returns>
    Task<ConfigurationMutationGroup?> GetAsync(string groupId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all history rows stamped with a mutation group identity.
    /// </summary>
    /// <param name="groupId">The group identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching history rows.</returns>
    Task<IReadOnlyList<ConfigurationValueHistory>> GetGroupHistoryAsync(string groupId, CancellationToken cancellationToken);
}
