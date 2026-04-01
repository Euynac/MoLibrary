namespace Monica.JobScheduler.Models;

/// <summary>
/// Consistency deviation details for a single job.
/// </summary>
public class JobConsistencyDeviation
{
    /// <summary>
    /// The job key.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// In-memory running count.
    /// </summary>
    public int MemoryRunningCount { get; init; }

    /// <summary>
    /// Database Processing state count.
    /// </summary>
    public int DatabaseProcessingCount { get; init; }

    /// <summary>
    /// In-memory pending reservation count.
    /// </summary>
    public int MemoryPendingCount { get; init; }

    /// <summary>
    /// Database Enqueued state count.
    /// </summary>
    public int DatabaseEnqueuedCount { get; init; }

    /// <summary>
    /// Running state deviation (Memory - Database).
    /// Positive means memory has more, negative means database has more.
    /// </summary>
    public int RunningDeviation => MemoryRunningCount - DatabaseProcessingCount;

    /// <summary>
    /// Pending state deviation (Memory - Database).
    /// </summary>
    public int PendingDeviation => MemoryPendingCount - DatabaseEnqueuedCount;

    /// <summary>
    /// Whether this job has any deviation.
    /// </summary>
    public bool HasDeviation => RunningDeviation != 0 || PendingDeviation != 0;
}
