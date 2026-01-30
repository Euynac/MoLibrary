using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Metadata;

/// <summary>
/// JobInstance 查询条件
/// </summary>
public record JobInstanceQuery
{
    /// <summary>
    /// JobKey 精确匹配
    /// </summary>
    public string? JobKey { get; init; }

    /// <summary>
    /// JobKey 批量精确匹配（优先级高于 JobKey 属性）
    /// </summary>
    public List<string>? JobKeys { get; init; }

    /// <summary>
    /// JobKey 模糊匹配（包含关系，不区分大小写）
    /// </summary>
    public string? JobKeyContains { get; init; }

    /// <summary>
    /// InstanceId 模糊匹配（包含关系，不区分大小写）
    /// </summary>
    public string? InstanceIdContains { get; init; }

    /// <summary>
    /// 按状态过滤（单一状态）
    /// </summary>
    public JobState? State { get; init; }

    /// <summary>
    /// 按多个状态过滤（优先级高于 State 属性）
    /// </summary>
    public List<JobState>? States { get; init; }

    /// <summary>
    /// 创建时间起始（包含）
    /// </summary>
    public DateTime? CreatedAfter { get; init; }

    /// <summary>
    /// 创建时间结束（包含）
    /// </summary>
    public DateTime? CreatedBefore { get; init; }

    /// <summary>
    /// 按创建时间排序方向
    /// </summary>
    public SortDirection SortByCreatedAt { get; init; } = SortDirection.Descending;

    /// <summary>
    /// 排序字段名称（支持: InstanceId, JobKey, State, CreatedAt, StartedAt, CompletedAt, Duration）
    /// </summary>
    public string? SortBy { get; init; }

    /// <summary>
    /// 是否降序排序
    /// </summary>
    public bool SortDescending { get; init; } = true;

    /// <summary>
    /// 页码（从 1 开始）
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize { get; init; } = 20;
}
