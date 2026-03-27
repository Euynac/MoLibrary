using System.Linq.Expressions;
using System.Reflection;

namespace Monica.Tool.Extensions;

public static class ExpressionExtensions
{
    /// <summary>
    /// Get the member name selected by the expression
    /// </summary>
    /// <param name="lambda"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static string GetMemberName(this LambdaExpression lambda)
    {
        string? memberName = null;
        var expression = lambda.Body;
        if (expression.NodeType == ExpressionType.MemberAccess)
        {
            var memberExpression = (MemberExpression) expression;
            memberName = memberExpression.Member.Name;
            expression = memberExpression.Expression;
        }
        if (memberName == null || (expression != null ? (expression.NodeType != ExpressionType.Parameter ? 1 : 0) : 1) != 0)
            throw new ArgumentException("Allow only first level member access (eg. obj => obj.Name)", nameof(lambda));
        return memberName;
    }

    /// <summary>
    /// Gets the value of the attribute selected in the given attribute selection expression from the given object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TReturn"></typeparam>
    /// <param name="expression"></param>
    /// <param name="obj"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static object? GetPropertyValue<T, TReturn>(this Expression<Func<T, TReturn>> expression, object? obj)
    {
        return GetPropertyInfo(expression).GetValue(obj);

        throw new ArgumentException($"Invalid expression. You should select property in {typeof(T).GetCleanFullName()}. (eg. obj => obj.Name)");
    }
    /// <summary>
    /// Gets information about the attribute selected in the given attribute selection expression
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TReturn"></typeparam>
    /// <param name="expression"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static PropertyInfo GetPropertyInfo<T, TReturn>(this Expression<Func<T, TReturn>> expression)
    {
        if (expression.Body is MemberExpression {Member: PropertyInfo propertyInfo1})
        {
            return propertyInfo1;
        }

        if (expression.Body is UnaryExpression {Operand: MemberExpression {Member: PropertyInfo propertyInfo2}})
        {
            return propertyInfo2;
        }

        throw new ArgumentException($"Invalid expression. You should select property in {typeof(T).GetCleanFullName()}. (eg. obj => obj.Name)");
    }
}