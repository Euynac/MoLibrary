using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using Monica.AutoModel.Configurations;
using Monica.AutoModel.Interfaces;
using Monica.AutoModel.Model;
using Monica.Tool.Extensions;
using Monica.Tool.General;

namespace Monica.AutoModel.Implements;

public class AutoModelMemoryOperatorDynamicLinqProvider<TModel> : AutoModelOperatorBase<TModel>, IAutoModelMemoryOperator<TModel>
    where TModel : class
{
    private readonly ParsingConfig _config = CreateParsingConfig();

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
    public Func<TModel, bool> GetFilter(string filter)
    {
        var result = _normalizer.NormalizeFilter(filter);
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
        return ApplyFilter(queryable, $"{selector.GetPropertyInfo().Name} {condition.GetKouEnumName()} \"{value}\""); // TODO: escape values?
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

    private static ParsingConfig CreateParsingConfig()
    {
        var config = new ParsingConfig();
        config.CustomTypeProvider = new LinqToObjectCustomProvider(config);
        return config;
    }
}

file class LinqToObjectCustomProvider(ParsingConfig config) : DefaultDynamicLinqCustomTypeProvider(config, [], true)
{
    public override HashSet<Type> GetCustomTypes()
    {
        var result = base.GetCustomTypes();
        result.Add(typeof(LinqToObjectFunctions));
        return result;
    }
}
