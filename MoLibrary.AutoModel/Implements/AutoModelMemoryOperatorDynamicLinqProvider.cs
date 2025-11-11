using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using MoLibrary.AutoModel.Configurations;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.AutoModel.Model;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.General;

namespace MoLibrary.AutoModel.Implements;

public class AutoModelMemoryOperatorDynamicLinqProvider<TModel> : AutoModelOperatorBase<TModel>, IAutoModelMemoryOperator<TModel>
    where TModel : class
{
    private readonly ParsingConfig _config = new()
    {
        CustomTypeProvider = new LinqToObjectCustomProvider()
    };

    private readonly IAutoModelExpressionNormalizer<TModel> _normalizer;

    public AutoModelMemoryOperatorDynamicLinqProvider(IAutoModelExpressionNormalizer<TModel> normalizer, IOptions<AutoModelExpressionOptions> options) : base(normalizer, options)
    {
        normalizer.SetToLinqToObject();
        _normalizer = normalizer;
    }

    public Func<TModel, bool> GetFilter(NormalizedResult result)
    {
        var func = (Func<TModel, bool>)DynamicExpressionParser.ParseLambda(_config, typeof(TModel), typeof(bool), result.FinalExpression,
            [.. result.Params]).Compile();
        return func;
    }

    public IEnumerable<TModel> ApplyFilter(IEnumerable<TModel> queryable, string filter)
    {
        var result = _normalizer.NormalizeFilter(filter);
        return ApplyFilter(queryable, result);
    }

    public IEnumerable<TModel> ApplyFilter(IEnumerable<TModel> queryable, NormalizedResult result)
    {
        return queryable.Where(GetFilter(result));
    }

    public virtual IEnumerable<TModel> ApplyFilter(IEnumerable<TModel> queryable, Expression<Func<TModel, object>> selector, EFieldConditions condition, string value)
    {
        return ApplyFilter(queryable, $"{selector.GetPropertyInfo().Name} {condition.GetKouEnumName()} \"{value}\""); //TODO 转义？
    }
    public IEnumerable<TModel> ApplyFuzzy(IEnumerable<TModel> queryable, string fuzzy, string? fuzzyColumns = null)
    {
        var result = _normalizer.NormalizeFuzzy(fuzzy, fuzzyColumns);
        return ApplyFilter(queryable, result);
    }

    public IEnumerable<dynamic> DynamicSelect(IEnumerable<TModel> queryable, string selectColumns)
    {
        var selector = _normalizer.NormalizeSelectColumns(selectColumns);
        dynamic func = DynamicExpressionParser.ParseLambda(_config, true, typeof(TModel), null, selector, null).Compile();
        return Enumerable.Select(queryable, func);
    }

    public IEnumerable<dynamic> DynamicSelectExcept(IEnumerable<TModel> queryable, string selectExceptColumns)
    {
        var selector = _normalizer.NormalizeSelectColumns(selectExceptColumns, true);
        dynamic func = DynamicExpressionParser.ParseLambda(_config, true, typeof(TModel), null, selector, null).Compile();
        return Enumerable.Select(queryable, func);
    }
}

file class LinqToObjectCustomProvider : DefaultDynamicLinqCustomTypeProvider
{
    public override HashSet<Type> GetCustomTypes()
    {
        var result = base.GetCustomTypes();
        result.Add(typeof(LinqToObjectFunctions));
        return result;
    }
}
