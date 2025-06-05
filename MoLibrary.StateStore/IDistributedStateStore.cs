using MoLibrary.StateStore.QueryBuilder.Interfaces;
using MoLibrary.StateStore.QueryBuilder;

namespace MoLibrary.StateStore;

public interface IDistributedStateStore : IStateStore
{
    /// <summary>
    /// 查询所有满足给定条件的状态
    /// </summary>
    /// <typeparam name="T">状态数据类型</typeparam>
    /// <param name="query">查询构建器函数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回满足条件的状态字典，键为状态键，值为状态数据</returns>
    Task<Dictionary<string, T?>> QueryStateAsync<T>(Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query, CancellationToken cancellationToken = default) where T : class;
}