namespace MoLibrary.JobScheduler.UI.Models;

using MoLibrary.JobScheduler.Models;

/// <summary>
/// 作业统计信息
/// </summary>
public class JobStatistics
{
    public string JobKey { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public TimeSpan? AverageExecutionTime { get; set; }
    public TimeSpan? FastestExecutionTime { get; set; }
    public string? FastestInstanceId { get; set; }
    public TimeSpan? SlowestExecutionTime { get; set; }
    public string? SlowestInstanceId { get; set; }
    public DateTime? LastExecutionTime { get; set; }
    public JobState? LastExecutionState { get; set; }
}
