namespace Monica.Configuration.Models;

/// <summary>
/// Describes the current target values and concurrency-bound plan for rolling back selected history rows.
/// </summary>
public sealed record ConfigurationHistoryRollbackPreview
{
    /// <summary>
    /// Gets the fingerprint that binds rollback apply to the current values reviewed by the operator.
    /// </summary>
    public required string PlanToken { get; init; }

    /// <summary>
    /// Gets the current physical target value for each history row identity.
    /// </summary>
    /// <remarks>
    /// A null value means that the path is currently absent. Rows sharing the same physical path intentionally
    /// repeat the same current value so UI grouping can remain history-oriented.
    /// </remarks>
    public IReadOnlyDictionary<string, ConfigurationStoredValue?> CurrentValuesByHistoryId { get; init; } =
        new Dictionary<string, ConfigurationStoredValue?>(StringComparer.OrdinalIgnoreCase);
}
