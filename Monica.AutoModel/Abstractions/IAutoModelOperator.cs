using Monica.AutoModel.Models;

namespace Monica.AutoModel.Abstractions;

/// <summary>
/// Common AutoModel field-resolution and normalization operations.
/// </summary>
public interface IAutoModelOperator<TModel>
{
    /// <summary>
    /// Resolves the specified field activation names to AutoModel fields.
    /// </summary>
    /// <param name="selectProperties">The field activation names to resolve.</param>
    /// <returns>The matching AutoModel field objects.</returns>
    List<AutoField> GetFields(params string[] selectProperties);
    /// <summary>
    /// Gets all AutoModel fields except the specified activation names.
    /// </summary>
    /// <param name="selectProperties">The field activation names to exclude.</param>
    /// <returns>The remaining AutoModel field objects.</returns>
    List<AutoField> GetFieldsExpect(params string[] selectProperties);

    /// <summary>
    /// Resolves a field-selection expression to AutoModel fields.
    /// </summary>
    /// <param name="selectExpression">The field-selection expression separated by <see cref="AutoModelExpressionOptions.SelectSeparator"/>.</param>
    /// <param name="isReverseSelect">Whether this is a reverse selection that excludes the specified fields.</param>
    /// <returns>The resolved AutoModel fields.</returns>
    List<AutoField> NormalizeLiteralSelect(string selectExpression, bool isReverseSelect = false);
    /// <summary>
    /// <inheritdoc cref="NormalizeLiteralSelect"/>
    /// </summary>
    /// <param name="selectExpression">The field-selection expression separated by <see cref="AutoModelExpressionOptions.SelectSeparator"/>.</param>
    /// <param name="isReverseSelect">Whether this is a reverse selection that excludes the specified fields.</param>
    /// <returns>A tuple containing the resolved fields and the field names that could not be resolved.</returns>
    (List<AutoField> fields, List<string> failedList) NormalizeLiteralSelectWithoutException(string selectExpression,
        bool isReverseSelect = false);

    /// <summary>
    /// Gets the normalized result of a filter expression.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <returns>The normalized filter result.</returns>
    NormalizedResult GetNormalizedResult(string filter);
}
