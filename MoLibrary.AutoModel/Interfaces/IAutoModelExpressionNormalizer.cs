using MoLibrary.Tool.General;

namespace MoLibrary.AutoModel.Interfaces;

/// <summary>
/// AutoModel表达式标准化器
/// </summary>
/// <typeparam name="TModel"></typeparam>
public interface IAutoModelExpressionNormalizer<TModel>
{
    /// <summary>
    /// 标准化选择指定字段表达式
    /// </summary>
    /// <param name="selectColumns"></param>
    /// <param name="isReverseSelect">是否是反向选择，即选择除了给定字段的字段</param>
    /// <returns></returns>
    string NormalizeSelectColumns(string selectColumns, bool isReverseSelect = false);

    /// <summary>
    /// 标准化过滤条件表达式
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    NormalizedResult NormalizeFilter(string filter);

    /// <summary>
    /// 标准化模糊查询表达式
    /// </summary>
    /// <param name="fuzzy"></param>
    /// <param name="fuzzyColumns"></param>
    /// <returns></returns>
    NormalizedResult NormalizeFuzzy(string fuzzy, string? fuzzyColumns = null);

    /// <summary>
    /// 将执行转化为Linq to object
    /// </summary>
    void SetToLinqToObject();
}

public class NormalizedResult(string finalExpression, List<object?> @params)
{
    public string FinalExpression { get; set; } = finalExpression;
    public List<object?> Params { get; set; } = @params;
    public override string ToString()
    {
        return $"Generated expression:{FinalExpression}\nparams:{Params.Select((p, i) =>
            new
            {
                Index = $"@{i}",
                Type = $"{p.GetType().Name}",
                Value = $"{p.ToJsonString()}"
            }).ToJsonString()}";
    }
}