using System;
using System.Linq.Expressions;
using System.Reflection;

namespace MoLibrary.Tool.Extensions;

public static class ExpressionExtensions
{
    public static PropertyInfo GetPropertyInfo<T, TReturn>(this Expression<Func<T, TReturn>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            return (PropertyInfo) memberExpression.Member;
        }

        if (expression.Body is UnaryExpression {Operand: MemberExpression memberExpression2})
        {
            return (PropertyInfo) memberExpression2.Member;
        }

        throw new ArgumentException("Invalid expression");
    }
}