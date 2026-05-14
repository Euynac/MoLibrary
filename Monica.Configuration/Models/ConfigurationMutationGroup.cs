namespace Monica.Configuration.Models;

/// <summary>
/// Represents a persisted batch of configuration mutations that should be audited and rolled back together.
/// </summary>
public sealed record ConfigurationMutationGroup
{
    /// <summary>
    /// Gets the group identity.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Gets the operator-facing group label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the optional operator reason for the batch.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the distinct definitions touched by this group.
    /// </summary>
    public IReadOnlyList<string> DefinitionKeys { get; init; } = [];

    /// <summary>
    /// Gets the number of successful mutations recorded in the group.
    /// </summary>
    public int MutationCount { get; init; }

    /// <summary>
    /// Gets when the group was created.
    /// </summary>
    public DateTimeOffset CreatedTime { get; init; }

    /// <summary>
    /// Gets the modifier identity.
    /// </summary>
    public string? ModifierId { get; init; }

    /// <summary>
    /// Gets the modifier display name.
    /// </summary>
    public string? ModifierName { get; init; }

    /// <summary>
    /// Gets when a later rollback was applied to this group.
    /// </summary>
    public DateTimeOffset? RolledBackTime { get; init; }

    /// <summary>
    /// Gets the mutation group that performed rollback for this group.
    /// </summary>
    public string? RolledBackGroupId { get; init; }

    /// <summary>
    /// Gets the persisted group status.
    /// </summary>
    public ConfigurationMutationGroupStatus Status { get; init; } = ConfigurationMutationGroupStatus.Applied;
}
