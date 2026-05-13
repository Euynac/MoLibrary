using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Provides configuration override values from one backing store.
/// </summary>
public interface IConfigurationValueSource
{
    /// <summary>
    /// Gets the source descriptor.
    /// </summary>
    ConfigurationSourceDescriptor Descriptor { get; }

    /// <summary>
    /// Loads all overrides from this source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source overrides.</returns>
    Task<IReadOnlyList<ConfigurationValueOverride>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets one override by logical path.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="logicalPath">The logical path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The effective override for the path in this source, or null when the source has none.</returns>
    Task<ConfigurationValueOverride?> GetAsync(string definitionKey, LogicalPath logicalPath, CancellationToken cancellationToken);

    /// <summary>
    /// Mutates this source atomically.
    /// </summary>
    /// <param name="mutation">The resolved source mutation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mutation result.</returns>
    /// <remarks>
    /// Writable sources own store-level invariant enforcement. A source must not persist an active
    /// container snapshot and active descendant leaf overrides for the same definition at the same time.
    /// </remarks>
    Task<ConfigurationMutationResult> MutateAsync(ConfigurationSourceMutation mutation, CancellationToken cancellationToken);
}
