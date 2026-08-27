namespace Monica.JobScheduler.Models.Definitions;

/// <summary>
/// Keeps stable definition ordering identical across persistence providers.
/// </summary>
internal static class JobDefinitionOrdering
{
    internal static IOrderedEnumerable<JobDefinition> ApplyDefinitionOrdering(
        this IEnumerable<JobDefinition> definitions,
        JobDefinitionQuery query)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(query);

        var ordered = query.SortField switch
        {
            JobDefinitionSortField.JobName => Order(
                definitions,
                static definition => definition.EffectiveConfiguration.JobName,
                query.SortDescending,
                StringComparer.OrdinalIgnoreCase),
            JobDefinitionSortField.JobKey => Order(
                definitions,
                static definition => definition.Declaration.JobKey,
                query.SortDescending,
                StringComparer.Ordinal),
            JobDefinitionSortField.OwnerKey => Order(
                definitions,
                static definition => definition.OwnerKey,
                query.SortDescending,
                StringComparer.Ordinal),
            JobDefinitionSortField.JobType => Order(
                definitions,
                static definition => definition.Declaration.JobType,
                query.SortDescending),
            JobDefinitionSortField.IsDisabled => Order(
                definitions,
                static definition => definition.IsDisabled,
                query.SortDescending),
            _ => throw new ArgumentOutOfRangeException(
                nameof(query),
                query.SortField,
                "Definition sort field is not supported.")
        };

        return ordered
            .ThenBy(static definition => definition.OwnerKey, StringComparer.Ordinal)
            .ThenBy(static definition => definition.Declaration.JobKey, StringComparer.Ordinal);
    }

    private static IOrderedEnumerable<JobDefinition> Order<TKey>(
        IEnumerable<JobDefinition> definitions,
        Func<JobDefinition, TKey> selector,
        bool descending,
        IComparer<TKey>? comparer = null) =>
        descending
            ? definitions.OrderByDescending(selector, comparer)
            : definitions.OrderBy(selector, comparer);
}
