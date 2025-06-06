using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Linq.Expressions;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.AutoModel.Model;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.General;

namespace MoLibrary.AutoModel.Implements;

public class AutoModelMemoryOperatorDynamicLinqProvider<TModel> : IAutoModelMemoryOperator<TModel>
    where TModel : class
{
    private readonly ParsingConfig _config = new()
    {
        CustomTypeProvider = new LinqToObjectCustomProvider()
    };

    private readonly IAutoModelExpressionNormalizer<TModel> _normalizer;

    public AutoModelMemoryOperatorDynamicLinqProvider(IAutoModelExpressionNormalizer<TModel> normalizer)
    {
        normalizer.SetToLinqToObject();
        _normalizer = normalizer;
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
        return queryable.Where(GetFilter(filter));
    }
    public virtual IEnumerable<TModel> ApplyFilter(IEnumerable<TModel> queryable, Expression<Func<TModel, object>> selector, EFieldConditions condition, string value)
    {
        return ApplyFilter(queryable, $"{selector.GetPropertyInfo().Name} {condition.GetKouEnumName()} \"{value}\""); //TODO 转义？
    }
    public IEnumerable<TModel> ApplyFuzzy(IEnumerable<TModel> queryable, string fuzzy, string? fuzzyColumns = null)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<dynamic> DynamicSelect(IEnumerable<TModel> queryable, string selectColumns)
    {
        var selector = _normalizer.NormalizeSelectColumns(selectColumns);
        var func = (dynamic)DynamicExpressionParser.ParseLambda(_config, true, typeof(TModel), null, selector, null).Compile();
        return Enumerable.Select(queryable, func);
    }

    public IEnumerable<dynamic> DynamicSelectExcept(IEnumerable<TModel> queryable, string selectExceptColumns)
    {
        throw new NotImplementedException();
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
