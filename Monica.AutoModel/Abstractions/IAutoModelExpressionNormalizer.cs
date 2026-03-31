using Monica.AutoModel.Models;

namespace Monica.AutoModel.Abstractions;

/// <summary>
/// Normalizes AutoModel select, filter, and fuzzy expressions.
/// </summary>
/// <typeparam name="TModel">The model type.</typeparam>
public interface IAutoModelExpressionNormalizer<TModel>
{
    /// <summary>
    /// Normalizes a field-selection expression.
    /// </summary>
    /// <param name="selectColumns">The field-selection expression.</param>
    /// <param name="isReverseSelect">Whether this is a reverse selection that excludes the specified fields.</param>
    /// <returns>The normalized Dynamic LINQ select expression.</returns>
    string NormalizeSelectColumns(string selectColumns, bool isReverseSelect = false);

    /// <summary>
    /// Normalizes a filter expression.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <returns>The normalized result.</returns>
    NormalizedResult NormalizeFilter(string filter);

    /// <summary>
    /// Normalizes a fuzzy-search request.
    /// </summary>
    /// <param name="fuzzy">The fuzzy-search value.</param>
    /// <param name="fuzzyColumns">Optional fields to include in fuzzy searching.</param>
    /// <returns>The normalized result.</returns>
    NormalizedResult NormalizeFuzzy(string fuzzy, string? fuzzyColumns = null);

    /// <summary>
    /// Switches subsequent normalization to LINQ to Objects.
    /// </summary>
    void SetToLinqToObject();

    /// <summary>
    /// Resolves a field-selection expression to AutoModel fields.
    /// </summary>
    /// <param name="columns">The field-selection expression separated by <see cref="AutoModelExpressionOptions.SelectSeparator"/>.</param>
    /// <param name="isReverseSelect">Whether this is a reverse selection that excludes the specified fields.</param>
    /// <returns>The resolved AutoModel fields.</returns>
    List<AutoField> NormalizeLiteralSelect(string columns, bool isReverseSelect = false);

    /// <summary>
    /// <inheritdoc cref="NormalizeLiteralSelect"/>
    /// </summary>
    /// <param name="selectExpression">The field-selection expression separated by <see cref="AutoModelExpressionOptions.SelectSeparator"/>.</param>
    /// <param name="isReverseSelect">Whether this is a reverse selection that excludes the specified fields.</param>
    /// <returns>A tuple containing the resolved fields and the field names that could not be resolved.</returns>
    (List<AutoField> fields, List<string> failedList) NormalizeLiteralSelectWithoutException(string selectExpression,
        bool isReverseSelect = false);
}
