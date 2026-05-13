namespace Monica.Configuration.Models;

/// <summary>
/// Represents a mutation after the core service has resolved schema, source, and projection metadata.
/// </summary>
public sealed record ConfigurationSourceMutation
{
    /// <summary>
    /// Gets the original caller request.
    /// </summary>
    public required ConfigurationMutationRequest Request { get; init; }

    /// <summary>
    /// Gets the resolved configuration definition.
    /// </summary>
    public required ConfigurationDefinition Definition { get; init; }

    /// <summary>
    /// Gets the schema node targeted by <see cref="Request"/>.
    /// </summary>
    public required ConfigurationNodeDefinition TargetNode { get; init; }

    /// <summary>
    /// Gets the source key selected for this mutation.
    /// </summary>
    public required string SourceKey { get; init; }

    /// <summary>
    /// Gets the projected Microsoft configuration path for the targeted logical path.
    /// </summary>
    public required string ConfigurationPath { get; init; }

    /// <summary>
    /// Gets whether the mutation targets one scalar value or a container snapshot.
    /// </summary>
    public ConfigurationOverrideGranularity Granularity { get; init; }
}
