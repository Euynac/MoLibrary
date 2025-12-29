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

    #endregion
}
