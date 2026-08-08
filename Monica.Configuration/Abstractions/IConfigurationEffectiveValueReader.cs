using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Reads the current effective configuration document for each managed configuration definition.
/// </summary>
/// <remarks>
/// Batch reads preserve the requested order and result count. This contract does not provide cross-process atomicity
/// or guarantee that a later read observes the same state.
/// </remarks>
public interface IConfigurationEffectiveValueReader
{
    /// <summary>
    /// Gets the store descriptor.
    /// </summary>
    ConfigurationStoreDescriptor Descriptor { get; }

    /// <summary>
    /// Gets one effective value document.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document, or null when the definition is not stored.</returns>
    Task<ConfigurationEffectiveValueDocument?> GetAsync(
        string definitionKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets effective value documents for several definitions in one store operation.
    /// </summary>
    /// <param name="definitionKeys">Definition keys in the required result order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// One entry for each requested definition, in the same order as <paramref name="definitionKeys"/>. A null entry
    /// means that the corresponding definition is not stored.
    /// </returns>
    Task<IReadOnlyList<ConfigurationEffectiveValueDocument?>> GetManyAsync(
        IReadOnlyList<string> definitionKeys,
        CancellationToken cancellationToken);
}
