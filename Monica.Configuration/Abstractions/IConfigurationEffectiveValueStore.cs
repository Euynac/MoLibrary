using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Persists the current effective configuration document for each managed configuration definition.
/// </summary>
public interface IConfigurationEffectiveValueStore
{
    /// <summary>
    /// Gets the store descriptor.
    /// </summary>
    ConfigurationStoreDescriptor Descriptor { get; }

    /// <summary>
    /// Ensures that a definition has an effective value document.
    /// </summary>
    /// <param name="definition">The configuration definition.</param>
    /// <param name="seedJson">The JSON document used when the definition is not present yet.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The existing or newly created effective value document.</returns>
    Task<ConfigurationEffectiveValueDocument> EnsureCreatedAsync(
        ConfigurationDefinition definition,
        string seedJson,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ensures that multiple definitions have effective value documents.
    /// </summary>
    /// <param name="seeds">Definitions and their seed JSON documents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Effective value documents in the same order as the input seeds.</returns>
    Task<IReadOnlyList<ConfigurationEffectiveValueDocument>> EnsureCreatedAsync(
        IReadOnlyList<ConfigurationEffectiveValueSeed> seeds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets one effective value document.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document, or null when the definition is not stored.</returns>
    Task<ConfigurationEffectiveValueDocument?> GetAsync(string definitionKey, CancellationToken cancellationToken);

    /// <summary>
    /// Gets effective value documents for several definitions in one store operation.
    /// </summary>
    /// <param name="definitionKeys">Definition keys in the required result order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Documents in the same order as <paramref name="definitionKeys"/>. A null entry means that the corresponding
    /// definition is not stored.
    /// </returns>
    Task<IReadOnlyList<ConfigurationEffectiveValueDocument?>> GetManyAsync(
        IReadOnlyList<string> definitionKeys,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves an updated effective value document.
    /// </summary>
    /// <param name="request">The save request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved document.</returns>
    Task<ConfigurationEffectiveValueDocument> SaveAsync(
        ConfigurationEffectiveValueSaveRequest request,
        CancellationToken cancellationToken);
}
