namespace Monica.Framework.UI.UITimekeeper.Models;

/// <summary>
/// Timekeeper statistics response model
/// </summary>
public class TimekeeperStatisticsResponse
{
    /// <summary>
    /// timer name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Number of executions
    /// </summary>
    public int Times { get; set; }

    /// <summary>
    /// average execution time
    /// </summary>
    public string Average { get; set; } = string.Empty;

    /// <summary>
    /// creation time
    /// </summary>
    public string CreateAt { get; set; } = string.Empty;

    /// <summary>
    /// Executions per minute
    /// </summary>
    public string TimesEveryMinutes { get; set; } = string.Empty;

    /// <summary>
    /// average memory usage
    /// </summary>
    public string? AverageMemory { get; set; }

    /// <summary>
    /// Last memory usage
    /// </summary>
    public string? LastMemory { get; set; }

    /// <summary>
    /// Last execution time
    /// </summary>
    public string LastDuration { get; set; } = string.Empty;

    /// <summary>
    /// last execution time
    /// </summary>
    public string? LastExecutedTime { get; set; }
}

/// <summary>
/// Running Timekeeper information response model
/// </summary>
public class RunningTimekeeperResponse
{
    /// <summary>
    /// timer name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Content description
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// start time
    /// </summary>
    public string StartTime { get; set; } = string.Empty;

    /// <summary>
    /// current elapsed time
    /// </summary>
    public string CurrentElapsed { get; set; } = string.Empty;

    /// <summary>
    /// Running time
    /// </summary>
    public string RunningDuration { get; set; } = string.Empty;
} 