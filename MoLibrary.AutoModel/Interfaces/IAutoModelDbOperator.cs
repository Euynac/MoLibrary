using MoLibrary.AutoModel.Model;
using System.Linq.Expressions;

namespace MoLibrary.AutoModel.Interfaces;

/// <summary>
/// 适用于数据库的自动模型功能接口
/// </summary>
/// <typeparam name="TModel"></typeparam>
public interface IAutoModelDbOperator<TModel>
{
    /// <summary>
    /// 应用过滤器
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="filter"></param>
    /// <returns></returns>
    IQueryable<TModel> ApplyFilter(IQueryable<TModel> queryable, string filter);

    /// <summary>
    /// 应用过滤器
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="selector"></param>
    /// <param name="condition"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    IQueryable<TModel> ApplyFilter(IQueryable<TModel> queryable, Expression<Func<TModel, object>> selector,
        EFieldConditions condition, string value);
    /// <summary>
    /// 应用模糊查询过滤器
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="fuzzy"></param>
    /// <param name="fuzzyColumns"></param>
    /// <returns></returns>
    IQueryable<TModel> ApplyFuzzy(IQueryable<TModel> queryable, string fuzzy, string? fuzzyColumns = null);

    /// <summary>
    /// 选择指定字段
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="selectColumns"></param>
    /// <returns></returns>
    IQueryable DynamicSelect(IQueryable<TModel> queryable, string selectColumns);
    /// <summary>
    /// 选择除了指定字段的字段
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="selectExceptColumns"></param>
    /// <returns></returns>
    IQueryable DynamicSelectExcept(IQueryable<TModel> queryable, string selectExceptColumns);
}