namespace Monica.Configuration.Models;

/// <summary>
/// Describes an effective value document save operation.
/// </summary>
public sealed record ConfigurationEffectiveValueSaveRequest
{
    /// <summary>
    /// Gets the configuration definition being saved.
    /// </summary>
    public required ConfigurationDefinition Definition { get; init; }

    /// <summary>
    /// Gets the updated JSON document.
    /// </summary>
    public required string Json { get; init; }

    /// <summary>
    /// Gets the expected document version for optimistic concurrency.
    /// </summary>
    public long? ExpectedVersion { get; init; }

    /// <summary>
    /// Gets the mutation context.
    /// </summary>
    public ConfigurationMutationContext Context { get; init; } = new();
}
