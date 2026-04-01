using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.UI.UIJobScheduler.Instances.Models;

/// <summary>
/// Job instance filter request
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
