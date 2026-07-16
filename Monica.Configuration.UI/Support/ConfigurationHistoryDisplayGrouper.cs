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
            .GroupBy(GroupKey, HistoryDisplayGroupKeyComparer.Instance)
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

    private static HistoryDisplayGroupKey GroupKey(ConfigurationValueHistory row)
    {
        if (!CanGroup(row))
        {
            return new HistoryDisplayGroupKey(
                row.HistoryId,
                MutationGroupId: null,
                DefinitionKey: null,
                row.TargetKind,
                ProviderType: null,
                DisplayName: null,
                PhysicalPath: null,
                LogicalPath: null);
        }

        return new HistoryDisplayGroupKey(
            UniqueHistoryId: null,
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

    private sealed record HistoryDisplayGroupKey(
        string? UniqueHistoryId,
        string? MutationGroupId,
        string? DefinitionKey,
        ConfigurationMutationTargetKind TargetKind,
        string? ProviderType,
        string? DisplayName,
        string? PhysicalPath,
        string? LogicalPath);

    private sealed class HistoryDisplayGroupKeyComparer : IEqualityComparer<HistoryDisplayGroupKey>
    {
        public static HistoryDisplayGroupKeyComparer Instance { get; } = new();

        public bool Equals(HistoryDisplayGroupKey? left, HistoryDisplayGroupKey? right)
        {
            return ReferenceEquals(left, right)
                   || left is not null
                   && right is not null
                   && left.TargetKind == right.TargetKind
                   && StringComparer.OrdinalIgnoreCase.Equals(left.UniqueHistoryId, right.UniqueHistoryId)
                   && StringComparer.OrdinalIgnoreCase.Equals(left.MutationGroupId, right.MutationGroupId)
                   && StringComparer.OrdinalIgnoreCase.Equals(left.DefinitionKey, right.DefinitionKey)
                   && StringComparer.OrdinalIgnoreCase.Equals(left.ProviderType, right.ProviderType)
                   && StringComparer.OrdinalIgnoreCase.Equals(left.DisplayName, right.DisplayName)
                   && StringComparer.OrdinalIgnoreCase.Equals(left.PhysicalPath, right.PhysicalPath)
                   && StringComparer.OrdinalIgnoreCase.Equals(left.LogicalPath, right.LogicalPath);
        }

        public int GetHashCode(HistoryDisplayGroupKey key)
        {
            var hash = new HashCode();
            hash.Add(key.TargetKind);
            hash.Add(key.UniqueHistoryId, StringComparer.OrdinalIgnoreCase);
            hash.Add(key.MutationGroupId, StringComparer.OrdinalIgnoreCase);
            hash.Add(key.DefinitionKey, StringComparer.OrdinalIgnoreCase);
            hash.Add(key.ProviderType, StringComparer.OrdinalIgnoreCase);
            hash.Add(key.DisplayName, StringComparer.OrdinalIgnoreCase);
            hash.Add(key.PhysicalPath, StringComparer.OrdinalIgnoreCase);
            hash.Add(key.LogicalPath, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
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
