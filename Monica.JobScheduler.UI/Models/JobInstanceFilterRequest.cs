namespace Monica.JobScheduler.UI.Models;

using Monica.JobScheduler.Models;

/// <summary>
/// 作业实例筛选请求
/// </summary>
public class JobInstanceFilterRequest
{
    public JobState? State { get; set; }
    public string? JobKey { get; set; }
    public string? InstanceId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = false;
}
