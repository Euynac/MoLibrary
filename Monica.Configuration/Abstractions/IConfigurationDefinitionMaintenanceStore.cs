using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Provides destructive maintenance operations for retired configuration definitions.
/// </summary>
/// <remarks>
/// Implementations must derive retirement from current publisher state and must preserve immutable cross-definition
/// audit records. Purge operations must atomically recheck publisher state and definition revision before deleting
/// the canonical definition, its publication history, and its current effective-value document.
/// </remarks>
public interface IConfigurationDefinitionMaintenanceStore
{
    /// <summary>
    /// Gets the records that would be deleted or retained when purging one definition.
    /// </summary>
    /// <param name="definitionKey">The stable definition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The purge impact preview.</returns>
    /// <exception cref="KeyNotFoundException">No persisted definition exists for the supplied key.</exception>
    Task<ConfigurationDefinitionPurgePreview> PreviewDefinitionPurgeAsync(
        string definitionKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Permanently removes one retired definition's mutable/current records.
    /// </summary>
    /// <param name="request">The key and reviewed definition revision.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">No persisted definition exists for the supplied key.</exception>
    /// <exception cref="ConfigurationConcurrencyConflictException">
    /// The definition is active or its revision no longer matches the reviewed revision.
    /// </exception>
    Task PurgeDefinitionAsync(
        ConfigurationDefinitionPurgeRequest request,
        CancellationToken cancellationToken);
}
