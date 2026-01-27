namespace MoLibrary.JobScheduler.Models;

/// <summary>
/// Result of a history cleanup operation.
/// </summary>
public class HistoryCleanupResult
{
    /// <summary>
    /// Total number of instances deleted.
    /// </summary>
    public int DeletedCount { get; set; }

    /// <summary>
    /// List of deleted instance IDs.
    /// </summary>
    public List<string> DeletedInstanceIds { get; set; } = [];

    /// <summary>
    /// Time taken for cleanup operation in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// When the cleanup was performed.
    /// </summary>
    public DateTime CleanupTime { get; set; }
}
