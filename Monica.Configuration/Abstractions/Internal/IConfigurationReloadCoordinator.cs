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
    /// Reloads host runtime configuration providers and then refreshes Monica's projection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReloadRuntimeConfigurationAsync(CancellationToken cancellationToken);
}
