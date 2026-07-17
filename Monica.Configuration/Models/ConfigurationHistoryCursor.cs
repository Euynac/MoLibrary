namespace Monica.Configuration.Models;

/// <summary>
/// Identifies one mutation unit in the deterministic newest-first history ordering.
/// </summary>
public sealed record ConfigurationHistoryCursor
{
    /// <summary>
    /// Gets the newest modification time within the mutation unit.
    /// </summary>
    public required DateTimeOffset ModifiedTime { get; init; }

    /// <summary>
    /// Gets the highest configuration version within the mutation unit.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets whether the unit represents a standalone history row or a persisted mutation group.
    /// </summary>
    public required ConfigurationHistoryUnitKind UnitKind { get; init; }

    /// <summary>
    /// Gets the non-empty history or mutation-group identity for the unit.
    /// </summary>
    public required string UnitId { get; init; }

    /// <summary>
    /// Validates that the cursor identifies a supported, concrete mutation unit.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the unit kind or identity is invalid.</exception>
    public void Validate()
    {
        if (!Enum.IsDefined(UnitKind))
        {
            throw new ArgumentException($"Unsupported history unit kind '{UnitKind}'.", nameof(UnitKind));
        }

        if (string.IsNullOrWhiteSpace(UnitId))
        {
            throw new ArgumentException("The history continuation cursor requires a unit identity.", nameof(UnitId));
        }
    }
}
