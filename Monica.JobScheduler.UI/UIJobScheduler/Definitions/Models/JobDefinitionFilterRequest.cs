using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.UI.UIJobScheduler.Definitions.Models;

/// <summary>
/// Job definition filter request
/// </summary>
public class JobDefinitionFilterRequest
{
    public string? FromProject { get; set; }
    public string? JobKey { get; set; }
    public string? JobName { get; set; }
    public JobType? JobType { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Sort field name
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Whether to sort in descending order
    /// </summary>
    public bool SortDescending { get; set; } = false;
}
