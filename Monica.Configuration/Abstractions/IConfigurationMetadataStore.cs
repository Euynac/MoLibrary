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
    /// Lists published definition records while isolating invalid persisted schemas per definition.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Independently materialized entries. Store-wide access, database-schema, and I/O failures still throw because
    /// the returned catalog would otherwise be indistinguishable from a complete result.
    /// </returns>
    Task<IReadOnlyList<ConfigurationPublishedDefinitionEntry>> ListPublishedDefinitionEntriesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets one published definition record without converting a persisted schema problem into not-found.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The independently materialized entry, or null when no metadata record exists.</returns>
    Task<ConfigurationPublishedDefinitionEntry?> GetPublishedDefinitionEntryAsync(
        string definitionKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists persisted schema publish history entries for one definition.
    /// </summary>
    /// <param name="definitionKey">The definition key to inspect.</param>
    /// <param name="limit">Maximum number of newest history entries to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Newest schema publish history entries first.</returns>
    Task<IReadOnlyList<ConfigurationDefinitionPublishHistory>> ListDefinitionPublishHistoriesAsync(
        string definitionKey,
        int limit,
        CancellationToken cancellationToken);
}
