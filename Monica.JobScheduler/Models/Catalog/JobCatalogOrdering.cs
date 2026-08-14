namespace Monica.JobScheduler.Models.Catalog;

/// <summary>
/// Keeps stable catalog ordering identical across persistence providers.
/// </summary>
internal static class JobCatalogOrdering
{
    internal static IOrderedEnumerable<ActiveJobDefinition> ApplyCatalogOrdering(
        this IEnumerable<ActiveJobDefinition> definitions,
        JobCatalogQuery query)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(query);

        var ordered = query.SortField switch
        {
            JobCatalogSortField.JobName => Order(
                definitions,
                static definition => definition.Declaration.JobName,
                query.SortDescending,
                StringComparer.OrdinalIgnoreCase),
            JobCatalogSortField.JobKey => Order(
                definitions,
                static definition => definition.Declaration.JobKey,
                query.SortDescending,
                StringComparer.Ordinal),
            JobCatalogSortField.OwnerId => Order(
                definitions,
                static definition => definition.OwnerId,
                query.SortDescending,
                StringComparer.Ordinal),
            JobCatalogSortField.JobType => Order(
                definitions,
                static definition => definition.Declaration.JobType,
                query.SortDescending),
            JobCatalogSortField.IsDisabled => Order(
                definitions,
                static definition => definition.IsDisabled,
                query.SortDescending),
            _ => throw new ArgumentOutOfRangeException(
                nameof(query),
                query.SortField,
                "Catalog sort field is not supported.")
        };

        return query.SortField == JobCatalogSortField.JobKey
            ? ordered
            : ordered.ThenBy(static definition => definition.Declaration.JobKey, StringComparer.Ordinal);
    }

    private static IOrderedEnumerable<ActiveJobDefinition> Order<TKey>(
        IEnumerable<ActiveJobDefinition> definitions,
        Func<ActiveJobDefinition, TKey> selector,
        bool descending,
        IComparer<TKey>? comparer = null) =>
        descending
            ? definitions.OrderByDescending(selector, comparer)
            : definitions.OrderBy(selector, comparer);
}
