namespace MoLibrary.JobScheduler.UI.Models;

using MoLibrary.JobScheduler.Models;

/// <summary>
/// 作业定义筛选请求
/// </summary>
public class JobDefinitionFilterRequest
{
    public string? FromProject { get; set; }
    public string? JobKey { get; set; }
    public string? JobName { get; set; }
    public JobType? JobType { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
