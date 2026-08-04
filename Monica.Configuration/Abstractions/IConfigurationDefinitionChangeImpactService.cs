using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Resolves the current logical publishers affected by configuration parameter changes.
/// </summary>
/// <remarks>
/// Results describe the latest publisher topology stored by Monica. They do not prove that a service is currently
/// alive, that a reload notification will be delivered, or that a changed feature will remain operational.
/// Publishers that explicitly report <see cref="ConfigurationReloadBehaviorObservationKind.NotConsumed"/> do not
/// participate, while unresolved possible consumers remain affected. A publisher state describes consumption of the
/// owning definition; it does not prove that the service reads every changed parameter. The nearest explicit
/// parameter or ancestor-node reload metadata overrides the publisher's definition-level observation.
/// </remarks>
public interface IConfigurationDefinitionChangeImpactService
{
    /// <summary>
    /// Gets current publisher impact for the supplied parameter changes.
    /// </summary>
    /// <param name="targets">Configuration parameters whose prospective changes are being reviewed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deterministic publisher impact without publication history.</returns>
    /// <exception cref="ArgumentException">A target collection or target identity is invalid.</exception>
    /// <exception cref="ConfigurationDefinitionNotFoundException">
    /// A target references a definition that is not currently active.
    /// </exception>
    /// <exception cref="ConfigurationValidationFailedException">
    /// A target path does not exist in its current definition schema.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// The metadata store omits requested publisher state or returns invalid impact metadata.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    Task<ConfigurationDefinitionChangeImpact> GetImpactAsync(
        IReadOnlyCollection<ConfigurationParameterChangeTarget> targets,
        CancellationToken cancellationToken);
}
