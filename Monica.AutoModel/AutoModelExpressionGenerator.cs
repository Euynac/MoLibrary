using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Monica.Tool.Extensions;

namespace Monica.AutoModel;

/// <summary>
/// Expression tree generator for AutoModel.
/// </summary>
internal class AutoModelExpressionGenerator
{

    /// <summary>
    /// Generates an order-by expression string.
    /// </summary>
    /// <param name="descend">Descending fields and their sort priorities.</param>
    /// <param name="ascend">Ascending fields and their sort priorities.</param>
    /// <returns>The combined order expression, or <c>null</c> when no sorting is specified.</returns>
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
    /// Generates a predicate delegate for delete, query, and existence-check operations.
    /// </summary>
    /// <param name="modelType">The target model type.</param>
    /// <param name="fieldCondition">The string-based condition expression.</param>
    /// <param name="fieldValues">The values referenced by the condition expression.</param>
    /// <returns>The generated predicate delegate, or <c>null</c> if the generic method cannot be resolved.</returns>
    public static object? GeneratePredicate(Type modelType, string fieldCondition, object?[]? fieldValues)
    {
        return typeof(AutoModelExpressionGenerator).GetMethod(nameof(GeneratePredicateGeneric))?
            .MakeGenericMethod(modelType).Invoke(null, new object?[] { $"cur => {fieldCondition}", fieldValues });
    }

    /// <summary>
    /// Generates a predicate delegate from a condition expression.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="fieldExpressions">The string-based condition expression.</param>
    /// <param name="fieldValues">The values referenced by the condition expression.</param>
    /// <returns>A compiled predicate delegate.</returns>
    public static Func<T, bool> GeneratePredicateGeneric<T>(string fieldExpressions, object[] fieldValues)
    {
        return (Func<T, bool>)DynamicExpressionParser
            .ParseLambda(typeof(T), typeof(bool), fieldExpressions, fieldValues).Compile();
    }

    /// <summary>
    /// Generates an assignment delegate for insert and update operations.
    /// </summary>
    /// <param name="modelType">The target model type.</param>
    /// <param name="fieldKeyValuePairs">The field-value pairs to assign.</param>
    /// <returns>The generated assignment delegate, or <c>null</c> if the generic method cannot be resolved.</returns>
    public static object? GenerateAction(Type modelType, IEnumerable<KeyValuePair<string, object>> fieldKeyValuePairs)
    {
        return typeof(AutoModelExpressionGenerator).GetMethod(nameof(GenerateActionGeneric))?
            .MakeGenericMethod(modelType).Invoke(null, new object[] { fieldKeyValuePairs });
    }
    /// <summary>
    /// Generates an assignment delegate for insert and update operations.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="fieldKeyValuePairs">The field-value pairs to assign.</param>
    /// <returns>A compiled assignment delegate.</returns>
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
