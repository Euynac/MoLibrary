using MoLibrary.JobScheduler.Metadata;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.Abstractions;

/// <summary>
/// Job 元数据仓储接口，提供 Job 定义和实例的持久化与查询能力
/// </summary>
/// <remarks>
/// 实现必须是线程安全的，并支持多种存储后端（内存、SQL、NoSQL 等）
/// </remarks>
public interface IMoJobMetadataRepository
{
    #region JobDefinition Operations

    /// <summary>
    /// 根据 JobKey 获取单个 Job 定义
    /// </summary>
    /// <param name="jobKey">Job 唯一标识符（通常是类型全名）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>找到返回 JobDefinition，否则返回 null</returns>
    Task<JobDefinition?> GetDefinitionAsync(string jobKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存 Job 定义（创建或更新）
    /// </summary>
    /// <param name="definition">要保存的 Job 定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentNullException">definition 为 null</exception>
    /// <exception cref="ArgumentException">definition.JobKey 为 null 或空</exception>
    Task SaveDefinitionAsync(JobDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询 Job 定义列表
    /// </summary>
    /// <param name="query">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>查询结果，包含项列表和总数</returns>
    Task<QueryResult<JobDefinition>> QueryDefinitionsAsync(
        JobDefinitionQuery query,
        CancellationToken cancellationToken = default);

    #endregion

    #region JobInstance Operations

    /// <summary>
    /// 根据实例 ID 获取单个 Job 实例
    /// </summary>
    /// <param name="instanceId">实例唯一标识符（GUID）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>找到返回 JobInstance，否则返回 null</returns>
    Task<JobInstance?> GetInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存 Job 实例（创建或更新）
    /// </summary>
    /// <param name="instance">要保存的 Job 实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentNullException">instance 为 null</exception>
    /// <exception cref="ArgumentException">instance.InstanceId 为 null 或空</exception>
    Task SaveInstanceAsync(JobInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询 Job 实例列表
    /// </summary>
    /// <param name="query">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>查询结果，包含项列表和总数</returns>
    Task<QueryResult<JobInstance>> QueryInstancesAsync(
        JobInstanceQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取多个作业的最后一次执行实例（优化N+1查询问题）
    /// </summary>
    /// <param name="jobKeys">作业键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>字典，Key为JobKey，Value为最后一次执行实例（如果没有则为null）</returns>
    Task<Dictionary<string, JobInstance?>> GetLatestInstancesAsync(
        IEnumerable<string> jobKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除 Job 实例
    /// </summary>
    /// <param name="instanceIds">要删除的实例 ID 集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功删除的实例数量</returns>
    /// <remarks>
    /// 实现应优雅处理部分失败，不存在的实例应被静默忽略
    /// </remarks>
    Task<int> DeleteInstancesAsync(
        IEnumerable<string> instanceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询需要清理的实例ID列表（优化的批量清理查询）
    /// </summary>
    /// <param name="retentionPolicies">作业保留策略字典 (JobKey -> (MaxRecords, MaxDays))</param>
    /// <param name="maxRetainedOrphanedInstances">孤立实例的最大保留记录数（默认10）</param>
    /// <param name="maxDeletionsPerCycle">每次清理最大删除数量限制（0=无限制）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>需要删除的实例ID列表</returns>
    /// <remarks>
    /// 实现应在数据库层面完成以下过滤：
    /// 1. 只考虑终态实例 (Succeeded, Terminated, Cancelled, Skipped, Failed)
    /// 2. 对每个JobKey，保留最近N条记录（N=MaxRecords）
    /// 3. 删除超过MaxDays天的记录
    /// 4. 孤立实例（JobKey不在retentionPolicies中）保留最近maxRetainedOrphanedInstances条
    /// </remarks>
    Task<List<string>> GetCleanupCandidatesAsync(
        IReadOnlyDictionary<string, (int MaxRecords, int? MaxDays)> retentionPolicies,
        int maxRetainedOrphanedInstances = 10,
        int maxDeletionsPerCycle = 0,
        CancellationToken cancellationToken = default);

    #endregion
}
