namespace Monica.JobScheduler.Metadata;

/// <summary>
/// JobDefinition 查询条件
/// </summary>
public record JobDefinitionQuery
{
    /// <summary>
    /// 按项目名称过滤
    /// </summary>
    public string? FromProject { get; init; }

    /// <summary>
    /// 是否包含已软删除的定义
    /// </summary>
    public bool IncludeDeleted { get; init; } = false;

    /// <summary>
    /// 页码（从 1 开始）
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize { get; init; } = 20;
}
