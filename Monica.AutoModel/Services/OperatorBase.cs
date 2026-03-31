using Microsoft.Extensions.Options;
using Monica.AutoModel.Abstractions;
using Monica.AutoModel.Models;
using Monica.Tool.Extensions;

namespace Monica.AutoModel.Services;

public abstract class OperatorBase<TModel>(IAutoModelExpressionNormalizer<TModel> normalizer, IOptions<AutoModelExpressionOptions> options) : IAutoModelOperator<TModel> where TModel : class
{
    public List<AutoField> GetFields(params string[] selectProperties)
    {
        return normalizer.NormalizeLiteralSelect(selectProperties.StringJoin(options.Value.SelectSeparator));
    }

    public List<AutoField> GetFieldsExpect(params string[] selectProperties)
    {
        return normalizer.NormalizeLiteralSelect(selectProperties.StringJoin(options.Value.SelectSeparator), true);
    }

    public List<AutoField> NormalizeLiteralSelect(string selectExpression, bool isReverseSelect = false)
    {
        return normalizer.NormalizeLiteralSelect(selectExpression, isReverseSelect);
    }
    public (List<AutoField> fields, List<string> failedList) NormalizeLiteralSelectWithoutException(string selectExpression, bool isReverseSelect = false)
    {
        return normalizer.NormalizeLiteralSelectWithoutException(selectExpression, isReverseSelect);
    }

    public NormalizedResult GetNormalizedResult(string filter)
    {
        return normalizer.NormalizeFilter(filter);
    }
}