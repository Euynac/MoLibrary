using BuildingBlocksPlatform.AutoModel.Model;
using BuildingBlocksPlatform.SeedWork;

namespace BuildingBlocksPlatform.AutoModel.Interfaces;

public interface IAutoModelExpressionTokenizer<TModel>
{
    /// <summary>
    /// Tokenize the expression
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    TokenizerContext Tokenize(string expression)
    {
        var context = new TokenizerContext(expression);
        ExtractComponent(context);
        return context;
    }

    void ExtractComponent(TokenizerContext context);
    bool NormalizeField(FieldToken token);
    bool NormalizeCondition(FieldToken token);
    void NormalizeValue(FieldToken token);

    NormalizedResult GenFinalExpression(TokenizerContext context);
}