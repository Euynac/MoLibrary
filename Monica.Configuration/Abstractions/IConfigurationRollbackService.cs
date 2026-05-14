using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Reverts configuration values from persisted mutation history.
/// </summary>
public interface IConfigurationRollbackService
{
    /// <summary>
    /// Rolls one history row back to its previous value.
    /// </summary>
    /// <param name="historyId">The history record identity.</param>
    /// <param name="context">Audit context for the rollback mutation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rollback mutation result.</returns>
    Task<ConfigurationMutationResult> RollbackHistoryAsync(
        string historyId,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rolls every mutation in a group back in reverse history order.
    /// </summary>
    /// <param name="groupId">The group identity.</param>
    /// <param name="context">Audit context for the rollback group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rollback mutation results.</returns>
    Task<IReadOnlyList<ConfigurationMutationResult>> RollbackGroupAsync(
        string groupId,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken);
}
