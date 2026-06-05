using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Persists published configuration definition metadata.
/// </summary>
public interface IConfigurationMetadataStore
{
    /// <summary>
    /// Gets the store descriptor.
    /// </summary>
    ConfigurationStoreDescriptor Descriptor { get; }

    /// <summary>
    /// Publishes the definitions owned by the current service.
    /// </summary>
    /// <param name="definitions">Definitions discovered from CLR configuration types.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(IReadOnlyList<ConfigurationDefinition> definitions, CancellationToken cancellationToken);

    /// <summary>
    /// Lists published definitions known by the metadata store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Published definitions with reconstructed runtime schema paths.</returns>
    Task<IReadOnlyList<ConfigurationDefinition>> ListPublishedDefinitionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets one published definition by key.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The published definition, or null when no metadata exists.</returns>
    Task<ConfigurationDefinition?> GetPublishedDefinitionAsync(string definitionKey, CancellationToken cancellationToken);
}
