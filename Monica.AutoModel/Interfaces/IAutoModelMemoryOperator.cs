using System.Linq.Expressions;
using Monica.AutoModel.Model;

namespace Monica.AutoModel.Interfaces;

/// <summary>
/// AutoModel operations for in-memory queries.
/// </summary>
/// <typeparam name="TModel">The model type.</typeparam>
public interface IAutoModelMemoryOperator<TModel> : IAutoModelOperator<TModel>
{
    /// <summary>
    /// Gets a filter predicate from a normalized result.
    /// </summary>
    /// <param name="result">The normalized filter result.</param>
    /// <returns>The generated predicate.</returns>
    Func<TModel, bool> GetFilter(NormalizedResult result);
    /// <summary>
    /// Gets a filter predicate from a filter expression.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <returns>The generated predicate.</returns>
    Func<TModel, bool> GetFilter(string filter);
    /// <summary>
    /// Applies a filter expression.
    /// </summary>
    /// <param name="queryable">The source sequence.</param>
    /// <param name="filter">The filter expression.</param>
    /// <returns>The filtered sequence.</returns>
    IEnumerable<TModel> ApplyFilter(IEnumerable<TModel> queryable, string filter);

    /// <summary>
    /// Applies a normalized filter.
    /// </summary>
    /// <param name="queryable">The source sequence.</param>
    /// <param name="result">The normalized filter result.</param>
    /// <returns>The filtered sequence.</returns>
    IEnumerable<TModel> ApplyFilter(IEnumerable<TModel> queryable, NormalizedResult result);
    /// <summary>
    /// Applies a filter for a single field.
    /// </summary>
    /// <param name="queryable">The source sequence.</param>
    /// <param name="selector">The field selector.</param>
    /// <param name="condition">The field condition.</param>
    /// <param name="value">The raw field value.</param>
    /// <returns>The filtered sequence.</returns>
    IEnumerable<TModel> ApplyFilter(IEnumerable<TModel> queryable, Expression<Func<TModel, object>> selector,
        EFieldConditions condition, string value);
    /// <summary>
    /// Applies a fuzzy-search filter.
    /// </summary>
    /// <param name="queryable">The source sequence.</param>
    /// <param name="fuzzy">The fuzzy-search value.</param>
    /// <param name="fuzzyColumns">Optional fields to include in fuzzy searching.</param>
    /// <returns>The filtered sequence.</returns>
    IEnumerable<TModel> ApplyFuzzy(IEnumerable<TModel> queryable, string fuzzy, string? fuzzyColumns = null);

    /// <summary>
    /// Selects the specified fields.
    /// </summary>
    /// <param name="queryable">The source sequence.</param>
    /// <param name="selectColumns">The selected fields.</param>
    /// <returns>A dynamically projected sequence.</returns>
    IEnumerable<dynamic> DynamicSelect(IEnumerable<TModel> queryable, string selectColumns);
    /// <summary>
    /// Selects all fields except the specified ones.
    /// </summary>
    /// <param name="queryable">The source sequence.</param>
    /// <param name="selectExceptColumns">The fields to exclude.</param>
    /// <returns>A dynamically projected sequence.</returns>
    IEnumerable<dynamic> DynamicSelectExcept(IEnumerable<TModel> queryable, string selectExceptColumns);
}
