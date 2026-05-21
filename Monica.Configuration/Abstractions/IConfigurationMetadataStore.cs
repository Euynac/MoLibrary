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
}
