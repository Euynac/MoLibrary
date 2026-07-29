namespace Monica.Core.HostedService.Abstractions;

/// <summary>
/// Coordinates readiness checkpoints between hosted services.
/// </summary>
public interface IMoHostedServiceCheckpointCoordinator
{
    /// <summary>
    /// Waits until the single registered instance of <typeparamref name="TService"/> signals the target checkpoint.
    /// </summary>
    /// <typeparam name="TService">The upstream hosted service type.</typeparam>
    /// <param name="checkpoint">The checkpoint name.</param>
    /// <param name="notBeforeUtc">Optional lower bound for the checkpoint occurrence time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no instance or more than one instance of <typeparamref name="TService"/> is registered.
    /// </exception>
    Task WaitForCheckpointAsync<TService>(
        string checkpoint,
        DateTime? notBeforeUtc = null,
        CancellationToken cancellationToken = default)
        where TService : IMoHostedService;

    /// <summary>
    /// Waits until the uniquely keyed hosted-service instance signals the target checkpoint.
    /// </summary>
    /// <typeparam name="TService">The concrete upstream hosted-service type.</typeparam>
    /// <param name="serviceKey">The exact service key.</param>
    /// <param name="checkpoint">The checkpoint name.</param>
    /// <param name="notBeforeUtc">Optional lower bound for the checkpoint occurrence time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the instance identity is not registered.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no service or more than one service matches.</exception>
    Task WaitForCheckpointAsync<TService>(
        string serviceKey,
        string checkpoint,
        DateTime? notBeforeUtc = null,
        CancellationToken cancellationToken = default)
        where TService : IMoHostedService;

    /// <summary>
    /// Waits until the exact hosted-service instance signals the target checkpoint.
    /// </summary>
    /// <param name="instanceId">The exact hosted-service instance identity.</param>
    /// <param name="checkpoint">The checkpoint name.</param>
    /// <param name="notBeforeUtc">Optional lower bound for the checkpoint occurrence time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WaitForCheckpointAsync(
        string instanceId,
        string checkpoint,
        DateTime? notBeforeUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals that the source hosted-service instance has reached a named checkpoint.
    /// </summary>
    /// <param name="source">The registered service instance that reached the checkpoint.</param>
    /// <param name="checkpoint">The checkpoint name.</param>
    /// <param name="occurredAtUtc">Optional checkpoint timestamp.</param>
    void SignalCheckpoint(IMoHostedService source, string checkpoint, DateTime? occurredAtUtc = null);
}
