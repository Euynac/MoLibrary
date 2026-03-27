using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.UI.Models;

/// <summary>
/// Lightweight Job instance projection, containing only the fields required for statistical analysis
/// Does not contain large text fields such as StateHistory and JobArgs to reduce data transfer
/// </summary>
public record JobInstanceStatistics
{
    /// <summary>
    /// Instance unique identifier
    /// </summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>
    /// job key
    /// </summary>
    public string JobKey { get; init; } = string.Empty;

    /// <summary>
    /// Execution status
    /// </summary>
    public JobState State { get; init; }

    /// <summary>
    /// creation time
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Start execution time
    /// </summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// completion time
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Number of retries
    /// </summary>
    public int RetryAttempt { get; init; }
}
