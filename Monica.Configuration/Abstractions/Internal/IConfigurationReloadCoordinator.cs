namespace Monica.Configuration.Abstractions.Internal;

/// <summary>
/// Coordinates reloads of the Microsoft configuration provider projection.
/// </summary>
internal interface IConfigurationReloadCoordinator
{
    /// <summary>
    /// Requests a provider reload.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReloadAsync(CancellationToken cancellationToken);
}
