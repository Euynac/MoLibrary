using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.Metadata;

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
    /// JobKey 模糊匹配（包含关系，不区分大小写）
    /// </summary>
    public string? JobKeyContains { get; init; }

    /// <summary>
    /// 按状态过滤
    /// </summary>
    public JobState? State { get; init; }

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
    /// 页码（从 1 开始）
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize { get; init; } = 20;
}
