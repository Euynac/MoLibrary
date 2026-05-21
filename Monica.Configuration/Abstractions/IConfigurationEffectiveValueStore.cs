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
    /// Gets one effective value document.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document, or null when the definition is not stored.</returns>
    Task<ConfigurationEffectiveValueDocument?> GetAsync(string definitionKey, CancellationToken cancellationToken);

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
