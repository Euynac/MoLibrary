using Microsoft.Extensions.Options;
using MoLibrary.AutoModel.Configurations;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.AutoModel.Model;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.AutoModel.Implements;

public abstract class AutoModelOperatorBase<TModel>(IAutoModelExpressionNormalizer<TModel> normalizer, IOptions<AutoModelExpressionOptions> options) : IAutoModelOperator<TModel> where TModel : class
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
}