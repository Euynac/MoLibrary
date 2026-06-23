namespace Monica.Configuration.Abstractions.Internal;

/// <summary>
/// Coordinates reloads of Monica and Microsoft configuration providers.
/// </summary>
internal interface IConfigurationReloadCoordinator
{
    /// <summary>
    /// Reloads only Monica's effective-value projection provider.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReloadMonicaProjectionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reloads one definition in Monica's effective-value projection provider.
    /// </summary>
    /// <param name="definitionKey">Definition key.</param>
    /// <param name="minimumVersion">Optional minimum version already observed by a reload signal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReloadMonicaProjectionAsync(string definitionKey, long? minimumVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the loaded Monica projection version for one definition.
    /// </summary>
    /// <param name="definitionKey">Definition key.</param>
    /// <returns>The loaded version, or null when unknown.</returns>
    long? GetLoadedMonicaProjectionVersion(string definitionKey);

    /// <summary>
    /// Reloads host runtime configuration providers and then refreshes Monica's projection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReloadRuntimeConfigurationAsync(CancellationToken cancellationToken);
}
