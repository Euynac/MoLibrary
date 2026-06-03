using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Builds operator-friendly history rows by visually collapsing scalar-heavy complex mutations.
/// </summary>
internal static class ConfigurationHistoryDisplayGrouper
{
    /// <summary>
    /// Collapses old scalar-heavy mutation groups into display rows without changing persisted history.
    /// </summary>
    /// <param name="rows">The persisted history rows.</param>
    /// <returns>Display rows ordered by latest mutation time.</returns>
    public static IReadOnlyList<ConfigurationHistoryDisplayRow> Collapse(IReadOnlyList<ConfigurationValueHistory> rows)
    {
        return rows
            .GroupBy(GroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildDisplayRow(group.ToArray()))
            .OrderByDescending(row => row.ModifiedTime)
            .ThenByDescending(row => row.Version)
            .ToArray();
    }

    private static ConfigurationHistoryDisplayRow BuildDisplayRow(IReadOnlyList<ConfigurationValueHistory> rows)
    {
        var ordered = rows
            .OrderByDescending(row => row.ModifiedTime)
            .ThenByDescending(row => row.Version)
            .ToArray();
        var representative = ordered[0];
        var displayPath = ShouldCollapse(ordered)
            ? TopLevelPath(representative.LogicalPath)
            : representative.LogicalPath;

        return new ConfigurationHistoryDisplayRow(
            representative.HistoryId,
            displayPath,
            ordered,
            ordered.Length > 1);
    }

    private static string GroupKey(ConfigurationValueHistory row)
    {
        if (!CanGroup(row))
        {
            return row.HistoryId;
        }

        return string.Join(
            '|',
            row.MutationGroupId,
            row.DefinitionKey,
            row.TargetKind,
            row.SourceProviderType,
            row.SourceDisplayName,
            row.SourcePhysicalPath,
            TopLevelPath(row.LogicalPath).ToCanonicalString());
    }

    private static bool CanGroup(ConfigurationValueHistory row)
    {
        return row.Granularity == ConfigurationMutationGranularity.Scalar
               && row.LogicalPath.Depth > 1
               && !string.IsNullOrWhiteSpace(row.MutationGroupId);
    }

    private static bool ShouldCollapse(IReadOnlyList<ConfigurationValueHistory> rows)
    {
        return rows.Count > 1 && rows.All(CanGroup);
    }

    private static LogicalPath TopLevelPath(LogicalPath path)
    {
        return path.Depth == 0
            ? LogicalPath.Root
            : new LogicalPath([path.Segments[0]]);
    }
}

/// <summary>
/// A history row as presented in the UI, possibly representing multiple persisted rows.
/// </summary>
/// <param name="Id">Stable UI row identity.</param>
/// <param name="DisplayPath">Path shown in the compact history list.</param>
/// <param name="Rows">Underlying persisted history rows.</param>
/// <param name="IsCollapsed">Whether this row visually collapses multiple persisted rows.</param>
internal sealed record ConfigurationHistoryDisplayRow(
    string Id,
    LogicalPath DisplayPath,
    IReadOnlyList<ConfigurationValueHistory> Rows,
    bool IsCollapsed)
{
    /// <summary>
    /// Gets the newest persisted row represented by this display row.
    /// </summary>
    public ConfigurationValueHistory Representative => Rows[0];

    /// <summary>
    /// Gets the newest mutation time represented by this display row.
    /// </summary>
    public DateTimeOffset ModifiedTime => Representative.ModifiedTime;

    /// <summary>
    /// Gets the newest version represented by this display row.
    /// </summary>
    public long Version => Rows.Max(row => row.Version);
}
