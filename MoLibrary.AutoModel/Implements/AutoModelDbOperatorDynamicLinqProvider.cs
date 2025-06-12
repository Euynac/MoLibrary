using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoLibrary.AutoModel.Configurations;
using MoLibrary.AutoModel.Exceptions;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.AutoModel.Model;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.General;

namespace MoLibrary.AutoModel.Implements;

public class AutoModelDbOperatorDynamicLinqProvider<TModel>(IAutoModelExpressionNormalizer<TModel> normalizer, IOptions<AutoModelExpressionOptions> options) : AutoModelOperatorBase<TModel>(normalizer, options), IAutoModelDbOperator<TModel>
    where TModel : class
{
    private readonly ParsingConfig _config = new()
    {
        CustomTypeProvider = new LinqToSqlCustomProvider(),
        AllowEqualsAndToStringMethodsOnObject = true //v1.6.0修复安全问题后需要设置该配置
    };

    private readonly IAutoModelExpressionNormalizer<TModel> _normalizer = normalizer;

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
        var result = _normalizer.NormalizeFilter(filter);
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
        var result = _normalizer.NormalizeFuzzy(fuzzy, fuzzyColumns);
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
        return queryable.Select(_normalizer.NormalizeSelectColumns(selectColumns));
    }

    public IQueryable DynamicSelectExcept(IQueryable<TModel> queryable, string selectExceptColumns)
    {
        return queryable.Select(_normalizer.NormalizeSelectColumns(selectExceptColumns, true));
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
