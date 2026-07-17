namespace Monica.Configuration.Models;

/// <summary>
/// Identifies one mutation group in the deterministic newest-first group ordering.
/// </summary>
public sealed record ConfigurationMutationGroupCursor
{
    /// <summary>
    /// Gets when the group was created.
    /// </summary>
    public required DateTimeOffset CreatedTime { get; init; }

    /// <summary>
    /// Gets the non-empty group identity.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Validates that the cursor identifies a concrete mutation group.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the group identity is empty.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(GroupId))
        {
            throw new ArgumentException("The mutation-group continuation cursor requires a group identity.", nameof(GroupId));
        }
    }
}
