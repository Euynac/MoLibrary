
using MoLibrary.Tool.Extensions;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;



namespace BuildingBlocksPlatform.AutoModel;

/// <summary>
/// 适用于AutoModel的表达式树生成器
/// </summary>
internal class AutoModelExpressionGenerator
{
 
    /// <summary>
    /// 生成排序规则字符串
    /// </summary>
    /// <param name="descend"></param>
    /// <param name="ascend"></param>
    /// <returns></returns>
    public static string? GenerateOrderConditionString(Dictionary<string, int>? descend, Dictionary<string, int>? ascend)
    {
        Dictionary<string, int> combinedList = new();
        if (descend != null)
        {
            foreach (var (fieldName, weight) in descend)
            {
                combinedList.AddOrReplace(fieldName + " desc", weight);
            }
        }

        if (ascend != null)
        {
            foreach (var (fieldName, weight) in ascend)
            {
                combinedList.AddOrReplace(fieldName, weight);
            }
        }

        return combinedList.Count != 0 ? string.Join(',', combinedList.OrderBy(tuple => tuple.Value).Select(p => p.Key)) : null;
    }

    /// <summary>
    /// 生成谓语委托，用于删、查、检查元组是否存在时使用
    /// </summary>
    /// <param name="modelType"></param>
    /// <param name="fieldCondition">字符串型条件</param>
    /// <param name="fieldValues">条件中对应的值</param>
    /// <returns></returns>
    public static object? GeneratePredicate(Type modelType, string fieldCondition, object?[]? fieldValues)
    {
        return typeof(AutoModelExpressionGenerator).GetMethod(nameof(GeneratePredicateGeneric))?
            .MakeGenericMethod(modelType).Invoke(null, new object?[] { $"cur => {fieldCondition}", fieldValues });
    }

    /// <summary>
    /// 生成谓语委托（即条件），用于删、查、检查元组是否存在时使用
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="fieldExpressions">字符串型条件</param>
    /// <param name="fieldValues">条件中对应的值</param>
    /// <returns></returns>
    public static Func<T, bool> GeneratePredicateGeneric<T>(string fieldExpressions, object[] fieldValues)
    {
        return (Func<T, bool>)DynamicExpressionParser
            .ParseLambda(typeof(T), typeof(bool), fieldExpressions, fieldValues).Compile();
    }

    /// <summary>
    /// 生成动作委托（即是赋值），在增，改元组时使用
    /// </summary>
    /// <param name="modelType"></param>
    /// <param name="fieldKeyValuePairs"></param>
    /// <returns></returns>
    public static object? GenerateAction(Type modelType, IEnumerable<KeyValuePair<string, object>> fieldKeyValuePairs)
    {
        return typeof(AutoModelExpressionGenerator).GetMethod(nameof(GenerateActionGeneric))?
            .MakeGenericMethod(modelType).Invoke(null, new object[] { fieldKeyValuePairs });
    }
    /// <summary>
    /// 生成动作委托（即是赋值），在增，改元组时使用
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="fieldKeyValuePairs"></param>
    /// <returns></returns>
    public static Action<T>? GenerateActionGeneric<T>(IEnumerable<KeyValuePair<string, object>> fieldKeyValuePairs) where T : new()
    {
        var p = Expression.Parameter(typeof(T), "p");
        if (fieldKeyValuePairs.IsNullOrEmptySet()) return null;
        var expressions = new List<Expression>();
        foreach (var (fieldName, fieldValue) in fieldKeyValuePairs)
        {
            Expression left = Expression.Property(p, fieldName);
            Expression right = Expression.Constant(fieldValue);
            Expression expression = Expression.Assign(left, right);
            expressions.Add(expression);
        }

        var body = Expression.Block(expressions);
        return Expression.Lambda<Action<T>>(body, p).Compile();
    }
}