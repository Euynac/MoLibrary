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
    /// Reconciles the complete definition-publication snapshot from one logical service.
    /// </summary>
    /// <param name="batch">The publisher identity, definitions, and service-local reload-behavior observations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(ConfigurationDefinitionPublicationBatch batch, CancellationToken cancellationToken);

    /// <summary>
    /// Retires every persisted observation owned by a logical publisher and recomputes affected aggregates.
    /// </summary>
    /// <remarks>
    /// Call this only when a logical service is intentionally decommissioned. Stores must not infer retirement from
    /// elapsed time because a temporary outage must not remove a restart requirement.
    /// </remarks>
    /// <param name="publisher">The logical publisher to retire and process identity performing the retirement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RetirePublisherAsync(ConfigurationPublisherIdentity publisher, CancellationToken cancellationToken);

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
    /// Gets current publisher state and persisted revision history for one definition.
    /// </summary>
    /// <param name="definitionKey">The definition key to inspect.</param>
    /// <param name="limit">Maximum number of newest history entries to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current publication overview with newest revisions first.</returns>
    /// <exception cref="KeyNotFoundException">No published metadata exists for <paramref name="definitionKey"/>.</exception>
    Task<ConfigurationDefinitionPublicationOverview> GetDefinitionPublicationOverviewAsync(
        string definitionKey,
        int limit,
        CancellationToken cancellationToken);
}
