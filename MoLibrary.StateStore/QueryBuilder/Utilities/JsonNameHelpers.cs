//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace MoLibrary.StateStore.QueryBuilder.Utilities;

public static class JsonNameHelpers<TValue>
{
    /// <summary>
    /// Gets the (potentially nested) JSON property name as indicated by the <see cref="JsonPropertyNameAttribute"/>
    /// attributes, if present and by the member name if not.
    /// </summary>
    /// <param name="expr">The property member expression.</param>
    /// <returns></returns>
    public static string GetJsonPropertyName<T>(Expression<Func<TValue, T>> expr)
    {
        if (expr.Body is not MemberExpression memberExpression)
            throw new ArgumentException("Invalid expression");

        if (memberExpression.Member is not PropertyInfo propertyInfo)
            throw new ArgumentException("Invalid expression");

        if (memberExpression.Expression is not MemberExpression)
        {
            return propertyInfo.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? propertyInfo.Name;
        }

        var parentJsonPropertyName =
            GetJsonPropertyName(memberExpression.Expression);
        return $"{parentJsonPropertyName}.{propertyInfo.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name}";
    }

    private static string GetJsonPropertyName(Expression expr)
    {
        if (expr is MemberExpression memberExpr)
        {
            var member = memberExpr.Member;
            return member.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? member.Name;
        }

        return string.Empty;
    }
}