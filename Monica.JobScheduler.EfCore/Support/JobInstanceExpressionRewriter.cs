using System.Linq.Expressions;
using System.Reflection;
using Monica.JobScheduler.EfCore.Entities;
using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.EfCore.Support;

/// <summary>
/// Converts Expression&lt;Func&lt;JobInstance, TResult&gt;&gt; to Expression&lt;Func&lt;JobInstanceEntity, TResult&gt;&gt;.
/// Since both types have identical property names, only parameter and member access replacements are required.
/// </summary>
internal sealed class JobInstanceExpressionRewriter : ExpressionVisitor
{
    private readonly ParameterExpression _oldParameter;
    private readonly ParameterExpression _newParameter;

    private JobInstanceExpressionRewriter(ParameterExpression oldParameter, ParameterExpression newParameter)
    {
        _oldParameter = oldParameter;
        _newParameter = newParameter;
    }

    protected override Expression VisitParameter(ParameterExpression node)
        => node == _oldParameter ? _newParameter : base.VisitParameter(node);

    protected override Expression VisitMember(MemberExpression node)
    {
        // Only rewrite member access on the old parameter
        if (node.Expression == _oldParameter && node.Member is PropertyInfo propertyInfo)
        {
            // Find corresponding property on JobInstanceEntity
            var entityProperty = typeof(JobInstanceEntity).GetProperty(propertyInfo.Name);
            if (entityProperty != null)
            {
                return Expression.Property(_newParameter, entityProperty);
            }
        }

        return base.VisitMember(node);
    }

    /// <summary>
    /// Rewrites a JobInstance projection expression into a JobInstanceEntity projection expression.
    /// </summary>
    /// <typeparam name="TResult">The projection result type.</typeparam>
    /// <param name="selector">A projection expression based on JobInstance.</param>
    /// <returns>The rewritten projection expression based on JobInstanceEntity.</returns>
    public static Expression<Func<JobInstanceEntity, TResult>> Rewrite<TResult>(
        Expression<Func<JobInstance, TResult>> selector)
    {
        var oldParam = selector.Parameters[0];
        var newParam = Expression.Parameter(typeof(JobInstanceEntity), oldParam.Name);
        var visitor = new JobInstanceExpressionRewriter(oldParam, newParam);
        var newBody = visitor.Visit(selector.Body);
        return Expression.Lambda<Func<JobInstanceEntity, TResult>>(newBody, newParam);
    }
}
