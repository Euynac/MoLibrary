using BuildingBlocksPlatform.AutoModel.Model;
using System.Linq.Expressions;

namespace BuildingBlocksPlatform.AutoModel.Interfaces;

/// <summary>
/// 适用于内存的自动模型功能接口
/// </summary>
/// <typeparam name="TModel"></typeparam>
public interface IAutoModelMemoryOperator<TModel>
{
    /// <summary>
    /// 获取过滤器
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    Func<TModel, bool> GetFilter(string filter);
    /// <summary>
    /// 应用过滤器
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="filter"></param>
    /// <returns></returns>
    IEnumerable<TModel> ApplyFilter(IEnumerable<TModel> queryable, string filter);

    /// <summary>
    /// 应用过滤器
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="selector"></param>
    /// <param name="condition"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    IEnumerable<TModel> ApplyFilter(IEnumerable<TModel> queryable, Expression<Func<TModel, object>> selector,
        EFieldConditions condition, string value);
    /// <summary>
    /// 应用模糊查询过滤器
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="fuzzy"></param>
    /// <param name="fuzzyColumns"></param>
    /// <returns></returns>
    IEnumerable<TModel> ApplyFuzzy(IEnumerable<TModel> queryable, string fuzzy, string? fuzzyColumns = null);

    /// <summary>
    /// 选择指定字段
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="selectColumns"></param>
    /// <returns></returns>
    IEnumerable<dynamic> DynamicSelect(IEnumerable<TModel> queryable, string selectColumns);
    /// <summary>
    /// 选择除了指定字段的字段
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="selectExceptColumns"></param>
    /// <returns></returns>
    IEnumerable<dynamic> DynamicSelectExcept(IEnumerable<TModel> queryable, string selectExceptColumns);
}