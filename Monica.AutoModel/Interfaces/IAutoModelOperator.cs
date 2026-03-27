using Monica.AutoModel.Configurations;
using Monica.AutoModel.Model;

namespace Monica.AutoModel.Interfaces;

public interface IAutoModelOperator<TModel>
{
    /// <summary>
    /// Converts selected field names to AutoModel field objects.
    /// </summary>
    /// <param name="selectProperties">The selected field names.</param>
    /// <returns>The matching AutoModel field objects.</returns>
    List<AutoField> GetFields(params string[] selectProperties);
    /// <summary>
    /// Converts selected field names to AutoModel field objects, excluding the specified fields.
    /// </summary>
    /// <param name="selectProperties">The field names to exclude.</param>
    /// <returns>The remaining AutoModel field objects.</returns>
    List<AutoField> GetFieldsExpect(params string[] selectProperties);

    /// <summary>
    /// Converts a selected-field expression into AutoModel field objects.
    /// </summary>
    /// <param name="selectExpression">The selected-field expression separated by <see cref="AutoModelExpressionOptions.SelectSeparator"/>.</param>
    /// <param name="isReverseSelect">Whether this is a reverse selection that excludes the specified fields.</param>
    /// <returns>The normalized AutoModel field objects.</returns>
    List<AutoField> NormalizeLiteralSelect(string selectExpression, bool isReverseSelect = false);
    /// <summary>
    /// <inheritdoc cref="NormalizeLiteralSelect"/>
    /// </summary>
    /// <param name="selectExpression"></param>
    /// <param name="isReverseSelect"></param>
    /// <returns></returns>
    (List<AutoField> fields, List<string> failedList) NormalizeLiteralSelectWithoutException(string selectExpression,
        bool isReverseSelect = false);

    /// <summary>
    /// Gets the normalized result of a filter expression.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <returns>The normalized filter result.</returns>
    NormalizedResult GetNormalizedResult(string filter);
}
