using Monica.AutoModel.Configurations;
using Monica.AutoModel.Model;
using Monica.Tool.General;

namespace Monica.AutoModel.Interfaces;

/// <summary>
/// AutoModel expression normalizer.
/// </summary>
/// <typeparam name="TModel">The model type.</typeparam>
public interface IAutoModelExpressionNormalizer<TModel>
{
    /// <summary>
    /// Normalizes a selected-field expression.
    /// </summary>
    /// <param name="selectColumns">The selected-field expression.</param>
    /// <param name="isReverseSelect">Whether this is a reverse selection that excludes the specified fields.</param>
    /// <returns>The normalized expression.</returns>
    string NormalizeSelectColumns(string selectColumns, bool isReverseSelect = false);

    /// <summary>
    /// Normalizes a filter expression.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <returns>The normalized result.</returns>
    NormalizedResult NormalizeFilter(string filter);

    /// <summary>
    /// Normalizes a fuzzy-search expression.
    /// </summary>
    /// <param name="fuzzy">The fuzzy-search value.</param>
    /// <param name="fuzzyColumns">Optional fields to include in fuzzy searching.</param>
    /// <returns>The normalized result.</returns>
    NormalizedResult NormalizeFuzzy(string fuzzy, string? fuzzyColumns = null);

    /// <summary>
    /// Switches execution to LINQ to Objects.
    /// </summary>
    void SetToLinqToObject();

    /// <summary>
    /// Converts a selected-field expression into AutoModel field objects.
    /// </summary>
    /// <param name="columns">The selected-field expression separated by <see cref="AutoModelExpressionOptions.SelectSeparator"/>.</param>
    /// <param name="isReverseSelect">Whether this is a reverse selection that excludes the specified fields.</param>
    /// <returns>The normalized AutoModel field objects.</returns>
    List<AutoField> NormalizeLiteralSelect(string columns, bool isReverseSelect = false);

    /// <summary>
    /// <inheritdoc cref="NormalizeLiteralSelect"/>
    /// </summary>
    /// <param name="selectExpression"></param>
    /// <param name="isReverseSelect"></param>
    /// <returns></returns>
    (List<AutoField> fields, List<string> failedList) NormalizeLiteralSelectWithoutException(string selectExpression,
        bool isReverseSelect = false);
}

public class NormalizedResult(string finalExpression, List<object?> @params, TokenizerContext context)
{
    public string FinalExpression { get; set; } = finalExpression;
    public List<object?> Params { get; set; } = @params;
    public TokenizerContext Context { get; } = context;

    public override string ToString()
    {
        return $"Generated expression:{FinalExpression}\nparams:{Params.Select((p, i) =>
            new
            {
                Index = $"@{i}",
                Type = p?.GetType().Name ?? "null",
                Value = p?.ToJsonString() ?? "null"
            }).ToJsonString()}";
    }
}
