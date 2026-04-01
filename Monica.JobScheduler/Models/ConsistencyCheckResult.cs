namespace Monica.JobScheduler.Models;

/// <summary>
/// Result of consistency check between in-memory state and database state.
/// </summary>
public class ConsistencyCheckResult
{
    /// <summary>
    /// Whether in-memory state is consistent with database state.
    /// </summary>
    public bool IsConsistent => TotalDeviation == 0;

    /// <summary>
    /// Total deviation count (sum of absolute deviations across all jobs).
    /// </summary>
    public int TotalDeviation { get; init; }

    /// <summary>
    /// Per-job deviation details.
    /// </summary>
    public required IReadOnlyList<JobConsistencyDeviation> Deviations { get; init; }

    /// <summary>
    /// Timestamp when the check was performed.
    /// </summary>
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
}
