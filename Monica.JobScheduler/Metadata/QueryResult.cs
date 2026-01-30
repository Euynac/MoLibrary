namespace Monica.JobScheduler.Metadata;

/// <summary>
/// 查询结果，包含分页数据和总数
/// </summary>
/// <typeparam name="T">结果项类型</typeparam>
/// <param name="Items">查询结果项列表</param>
/// <param name="TotalCount">总记录数（未分页前）</param>
public record QueryResult<T>(List<T> Items, int TotalCount);
