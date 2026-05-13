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
    /// <returns>The override, or null when the source has none.</returns>
    Task<ConfigurationValueOverride?> GetAsync(string definitionKey, LogicalPath logicalPath, CancellationToken cancellationToken);

    /// <summary>
    /// Mutates this source.
    /// </summary>
    /// <param name="request">The mutation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mutation result.</returns>
    Task<ConfigurationMutationResult> MutateAsync(ConfigurationMutationRequest request, CancellationToken cancellationToken);
}
