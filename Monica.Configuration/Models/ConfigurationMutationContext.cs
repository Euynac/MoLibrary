namespace Monica.Configuration.Models;

/// <summary>
/// Carries audit context for a configuration mutation.
/// </summary>
public sealed record ConfigurationMutationContext
{
    /// <summary>
    /// Gets the modifier identity.
    /// </summary>
    public string? ModifierId { get; init; }

    /// <summary>
    /// Gets the modifier display name.
    /// </summary>
    public string? ModifierName { get; init; }

    /// <summary>
    /// Gets the mutation reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the persisted mutation group identity that this mutation belongs to.
    /// </summary>
    public string? MutationGroupId { get; init; }
}
