namespace Monica.Configuration.Models;

/// <summary>
/// Describes a unified configuration version snapshot to persist.
/// </summary>
public sealed record ConfigurationUnifiedVersionCreateRequest
{
    /// <summary>
    /// Gets the mutation group that triggered this version, if any.
    /// </summary>
    public string? MutationGroupId { get; init; }

    /// <summary>
    /// Gets the definition keys that triggered capture.
    /// </summary>
    public IReadOnlyList<string> TriggerDefinitionKeys { get; init; } = [];

    /// <summary>
    /// Gets the capture time.
    /// </summary>
    public DateTimeOffset CreatedTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the modifier identity.
    /// </summary>
    public string? ModifierId { get; init; }

    /// <summary>
    /// Gets the modifier display name.
    /// </summary>
    public string? ModifierName { get; init; }

    /// <summary>
    /// Gets the optional reason supplied by the operator.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the captured definition documents.
    /// </summary>
    public IReadOnlyList<ConfigurationUnifiedVersionDefinitionSnapshot> Definitions { get; init; } = [];
}
