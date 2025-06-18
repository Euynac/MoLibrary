using MoLibrary.AutoModel.Configurations;
using MoLibrary.AutoModel.Model;

namespace MoLibrary.AutoModel.Interfaces;

public interface IAutoModelOperator<TModel>
{
    /// <summary>
    /// 将选择字段转换为自动模型字段对象
    /// </summary>
    /// <param name="selectProperties"></param>
    /// <returns></returns>
    List<AutoField> GetFields(params string[] selectProperties);
    /// <summary>
    /// 将选择字段转换为自动模型字段对象
    /// </summary>
    /// <param name="selectProperties"></param>
    /// <returns></returns>
    List<AutoField> GetFieldsExpect(params string[] selectProperties);

    /// <summary>
    /// 将选择字段表达式转换为自动模型字段对象，需要使用 <see cref="AutoModelExpressionOptions.SelectSeparator"/> 分割
    /// </summary>
    /// <param name="selectExpression"></param>
    /// <param name="isReverseSelect">是否是反向选择，即选择除了给定字段的字段</param>
    /// <returns></returns>
    List<AutoField> NormalizeLiteralSelect(string selectExpression, bool isReverseSelect = false);
    /// <summary>
    /// <inheritdoc cref="NormalizeLiteralSelect"/>
    /// </summary>
    /// <param name="selectExpression"></param>
    /// <param name="isReverseSelect"></param>
    /// <returns></returns>
    (List<AutoField> fields, List<string> failedList) NormalizeLiteralSelectWithoutException(string selectExpression,
        bool isReverseSelect = false);
}