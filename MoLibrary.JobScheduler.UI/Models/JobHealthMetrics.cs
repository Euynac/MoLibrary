namespace MoLibrary.JobScheduler.UI.Models;

using MoLibrary.JobScheduler.Models;

/// <summary>
/// 作业健康指标
/// </summary>
public class JobHealthMetrics
{
    public string JobKey { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double FailureRate => TotalExecutions > 0
        ? (FailedExecutions / (double)TotalExecutions) * 100
        : 0;
    public List<JobInstance> RecentFailures { get; set; } = new();
    public DateTime MetricsStartTime { get; set; }
    public DateTime MetricsEndTime { get; set; }
}
