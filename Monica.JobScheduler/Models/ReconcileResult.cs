namespace Monica.JobScheduler.Models;

/// <summary>
/// Result of reconciliation operation.
/// </summary>
public class ReconcileResult
{
    /// <summary>
    /// Whether the reconciliation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if reconciliation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// State before reconciliation.
    /// </summary>
    public ConsistencyCheckResult? StateBefore { get; init; }

    /// <summary>
    /// State after reconciliation.
    /// </summary>
    public ConsistencyCheckResult? StateAfter { get; init; }

    /// <summary>
    /// Timestamp when reconciliation was performed.
    /// </summary>
    public DateTime ReconciledAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Duration of the reconciliation operation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    public static ReconcileResult Ok(ConsistencyCheckResult before, ConsistencyCheckResult after, TimeSpan duration)
        => new() { Success = true, StateBefore = before, StateAfter = after, Duration = duration };

    public static ReconcileResult Fail(string error)
        => new() { Success = false, ErrorMessage = error };
}
