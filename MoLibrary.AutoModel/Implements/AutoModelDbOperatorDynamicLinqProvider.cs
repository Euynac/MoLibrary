using Microsoft.EntityFrameworkCore;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.AutoModel.Model;
using MoLibrary.Tool.Extensions;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Linq.Expressions;
using MoLibrary.AutoModel.Exceptions;
using MoLibrary.Tool.General;

namespace MoLibrary.AutoModel.Implements;

public class AutoModelDbOperatorDynamicLinqProvider<TModel>
    (IAutoModelExpressionNormalizer<TModel> normalizer) : IAutoModelDbOperator<TModel>
    where TModel : class
{
    private readonly ParsingConfig _config = new()
    {
        CustomTypeProvider = new LinqToSqlCustomProvider()
    };
    public virtual IQueryable<TModel> ApplyFilter(IQueryable<TModel> queryable, Expression<Func<TModel, object>> selector, EFieldConditions condition, string value)
    {
        return ApplyFilter(queryable, $"{selector.GetPropertyInfo().Name} {condition.GetKouEnumName()} \"{value}\""); //TODO 转义？
    }
    public virtual IQueryable<TModel> ApplyFilter(IQueryable<TModel> queryable, string filter)
    {
        //测试后门
        if (filter.StartsWith('[') && filter.EndsWith(']'))
        {
            return queryable.Where(_config, filter.TrimStart('[').TrimEnd(']'));
        }
        var result = normalizer.NormalizeFilter(filter);
        try
        {
            var query = queryable.Where(_config, result.FinalExpression, [.. result.Params]);
            return query;
        }
        catch (Exception e)
        {
            throw new AutoModelInvokerException($"{result}执行SQL生成出现错误：{e.Message}");
        }
    }

    public virtual IQueryable<TModel> ApplyFuzzy(IQueryable<TModel> queryable, string fuzzy, string? fuzzyColumns = null)
    {
        var result = normalizer.NormalizeFuzzy(fuzzy, fuzzyColumns);
        try
        {
            var query = queryable.Where(_config, result.FinalExpression, [.. result.Params]);
            return query;
        }
        catch (Exception e)
        {
            throw new AutoModelInvokerException($"{result}执行SQL生成出现错误：{e.Message}");
        }
    }

    public virtual IQueryable DynamicSelect(IQueryable<TModel> queryable, string selectColumns)
    {
        return queryable.Select(normalizer.NormalizeSelectColumns(selectColumns));
    }

    public IQueryable DynamicSelectExcept(IQueryable<TModel> queryable, string selectExceptColumns)
    {
        return queryable.Select(normalizer.NormalizeSelectColumns(selectExceptColumns, true));
    }
}

file class LinqToSqlCustomProvider : DefaultDynamicLinqCustomTypeProvider
{
    public override HashSet<Type> GetCustomTypes()
    {
        var result = base.GetCustomTypes();
        result.Add(typeof(EF));
        //result.Add(typeof(NpgsqlFullTextSearchDbFunctionsExtensions));
        //result.Add(typeof(NpgsqlDbFunctionsExtensions));
        result.Add(typeof(DbFunctionsExtensions));
        //result.Add(typeof(DbFunctions));
        return result;
    }
}
