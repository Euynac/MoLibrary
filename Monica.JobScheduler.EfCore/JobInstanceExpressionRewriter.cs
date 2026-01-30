using System.Linq.Expressions;
using System.Reflection;
using Monica.JobScheduler.EfCore.Entities;
using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.EfCore;

/// <summary>
/// 将 Expression&lt;Func&lt;JobInstance, TResult&gt;&gt; 转换为 Expression&lt;Func&lt;JobInstanceEntity, TResult&gt;&gt;
/// 由于两个类型的属性名称完全相同，只需替换参数类型和成员访问
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
    /// 将 JobInstance 的投影表达式重写为 JobInstanceEntity 的投影表达式
    /// </summary>
    /// <typeparam name="TResult">投影结果类型</typeparam>
    /// <param name="selector">基于 JobInstance 的投影表达式</param>
    /// <returns>重写后的基于 JobInstanceEntity 的投影表达式</returns>
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
