using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Reverts configuration values from persisted mutation history.
/// </summary>
public interface IConfigurationRollbackService
{
    /// <summary>
    /// Builds a current-state rollback preview for selected history rows.
    /// </summary>
    /// <param name="historyIds">The history identities to preview.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current physical values and a concurrency-bound plan token.</returns>
    Task<ConfigurationHistoryRollbackPreview> PreviewHistoriesAsync(
        IReadOnlyList<string> historyIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rolls one history row back to its previous value.
    /// </summary>
    /// <param name="historyId">The history record identity.</param>
    /// <param name="planToken">The current-state preview token reviewed by the operator.</param>
    /// <param name="context">Audit context for the rollback mutation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rollback mutation result.</returns>
    Task<ConfigurationMutationResult> RollbackHistoryAsync(
        string historyId,
        string planToken,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rolls selected history rows back in reverse history order.
    /// </summary>
    /// <param name="historyIds">The history record identities to roll back.</param>
    /// <param name="planToken">The current-state preview token reviewed by the operator.</param>
    /// <param name="context">Audit context for the rollback group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rollback mutation results.</returns>
    Task<IReadOnlyList<ConfigurationMutationResult>> RollbackHistoriesAsync(
        IReadOnlyList<string> historyIds,
        string planToken,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rolls every mutation in a group back in reverse history order.
    /// </summary>
    /// <param name="groupId">The group identity.</param>
    /// <param name="planToken">The current-state preview token reviewed by the operator.</param>
    /// <param name="context">Audit context for the rollback group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rollback mutation results.</returns>
    Task<IReadOnlyList<ConfigurationMutationResult>> RollbackGroupAsync(
        string groupId,
        string planToken,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken);
}
