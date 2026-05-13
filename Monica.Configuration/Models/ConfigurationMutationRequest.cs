namespace Monica.Configuration.Models;

/// <summary>
/// Describes a requested configuration value mutation.
/// </summary>
public sealed record ConfigurationMutationRequest
{
    /// <summary>
    /// Gets the target definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the target logical path.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }

    /// <summary>
    /// Gets the mutation kind.
    /// </summary>
    public ConfigurationMutationKind MutationKind { get; init; }

    /// <summary>
    /// Gets the value payload.
    /// </summary>
    public required ConfigurationStoredValue Value { get; init; }

    /// <summary>
    /// Gets the target source key. When omitted, the default writable source is used.
    /// </summary>
    public string? TargetSourceKey { get; init; }

    /// <summary>
    /// Gets the expected schema version.
    /// </summary>
    public int ExpectedSchemaVersion { get; init; }

    /// <summary>
    /// Gets the expected value version for optimistic concurrency.
    /// </summary>
    public long? ExpectedValueVersion { get; init; }

    /// <summary>
    /// Gets the mutation context.
    /// </summary>
    public ConfigurationMutationContext Context { get; init; } = new();
}
