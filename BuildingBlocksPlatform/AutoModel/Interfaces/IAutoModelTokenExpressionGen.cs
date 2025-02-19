using BuildingBlocksPlatform.AutoModel.Model;

namespace BuildingBlocksPlatform.AutoModel.Interfaces;

public interface IAutoModelTokenExpressionGen
{
    /// <summary>
    /// 按照单个字段的条件生成对应的表达式
    /// </summary>
    /// <param name="token"></param>
    /// <param name="num">是第几个字段</param>
    /// <param name="totalParamCount"></param>
    /// <param name="supplementParamObjects"></param>
    /// <remarks>https://dynamic-linq.net/expression-language#calling-method-and-constructor</remarks>
    /// <returns></returns>
    string GenerateTokenExpression(FieldToken token, int num, int totalParamCount,
        out List<object> supplementParamObjects);

}