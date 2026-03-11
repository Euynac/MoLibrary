namespace Monica.Core.Features.HostedServices.Interfaces;

/// <summary>
/// Coordinates readiness checkpoints between hosted services.
/// </summary>
public interface IMoHostedServiceDependencyCoordinator
{
    /// <summary>
    /// Waits until the specified hosted service signals the target checkpoint.
    /// </summary>
    /// <typeparam name="TService">The upstream hosted service type.</typeparam>
    /// <param name="checkpoint">The checkpoint name.</param>
    /// <param name="notBeforeUtc">Optional lower bound for the checkpoint occurrence time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WaitForCheckpointAsync<TService>(
        string checkpoint,
        DateTime? notBeforeUtc = null,
        CancellationToken cancellationToken = default)
        where TService : IMoHostedService;

    /// <summary>
    /// Signals that a hosted service has reached a named checkpoint.
    /// </summary>
    /// <param name="serviceType">The hosted service type that reached the checkpoint.</param>
    /// <param name="checkpoint">The checkpoint name.</param>
    /// <param name="occurredAtUtc">Optional checkpoint timestamp.</param>
    void SignalCheckpoint(Type serviceType, string checkpoint, DateTime? occurredAtUtc = null);
}
