using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Resolves the current logical publishers affected by configuration definition changes.
/// </summary>
/// <remarks>
/// Results describe the latest publisher topology stored by Monica. They do not prove that a service is currently
/// alive, that a reload notification will be delivered, or that a changed feature will remain operational.
/// Publishers that explicitly report <see cref="ConfigurationReloadBehaviorObservationKind.NotConsumed"/> do not
/// participate, while unresolved possible consumers remain affected.
/// </remarks>
public interface IConfigurationDefinitionChangeImpactService
{
    /// <summary>
    /// Gets current publisher impact for the supplied definition keys.
    /// </summary>
    /// <param name="definitionKeys">Definition keys whose prospective changes are being reviewed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deterministic publisher impact without publication history.</returns>
    /// <exception cref="ArgumentException">No non-empty definition key was supplied.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    Task<ConfigurationDefinitionChangeImpact> GetImpactAsync(
        IReadOnlyCollection<string> definitionKeys,
        CancellationToken cancellationToken);
}
