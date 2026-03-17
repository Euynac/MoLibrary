using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Events;

/// <summary>
/// Event published when job definitions are reconciled during application startup or reload.
/// Allows subscribers to update their in-memory state to reflect the current job definitions.
/// </summary>
public class JobDefinitionsChangedEvent
{
    public required string SchedulerScopeKey { get; init; }
    public required string FromProject { get; init; }//TODO change to FromClientId
    /// <summary>
    /// The current list of all active job definitions after reconciliation
    /// </summary>
    public required IReadOnlyList<JobDefinition> AddedDefinitions { get; init; }

    /// <summary>
    /// Job keys that were newly added
    /// </summary>
    public required IReadOnlyList<string> AddedJobKeys { get; init; }

    /// <summary>
    /// Job keys that were soft deleted (marked as deleted)
    /// </summary>
    public required IReadOnlyList<string> DeletedJobKeys { get; init; }

    /// <summary>
    /// Job definitions that were updated (property changes like IsDisabled, CronExpression, etc.)
    /// </summary>
    public IReadOnlyList<JobDefinition> UpdatedDefinitions { get; init; } = [];

    /// <summary>
    /// Job keys that were updated
    /// </summary>
    public IReadOnlyList<string> UpdatedJobKeys { get; init; } = [];

    /// <summary>
    /// Timestamp when the reconciliation occurred
    /// </summary>
    public required DateTime ReconciledAt { get; init; }
}
