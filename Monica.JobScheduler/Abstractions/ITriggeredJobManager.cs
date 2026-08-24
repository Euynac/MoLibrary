namespace Monica.JobScheduler.Abstractions;

/// <summary>
///     Defines interface of a job manager.
/// </summary>
public interface ITriggeredJobManager
{
    /// <summary>
    ///     Enqueues a job to be executed.
    /// </summary>
    /// <typeparam name="TArgs">Type of the arguments of job.</typeparam>
    /// <param name="args">Job arguments.</param>
    /// <param name="delay">Job delay (wait duration before first try).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Unique identifier of a background job.</returns>
    Task<string> EnqueueAsync<TArgs>(
        TArgs args,
        TimeSpan? delay = null, CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Cancels a queued execution immediately or requests cooperative cancellation from a running execution.
    /// </summary>
    /// <param name="instanceId">The instance ID returned from EnqueueAsync.</param>
    /// <param name="cancellationToken">Cancellation token for the durable store operation.</param>
    /// <returns>The durable cancellation outcome.</returns>
    Task<Models.Execution.JobCancellationResult> CancelExecutionAsync(
        string instanceId,
        CancellationToken cancellationToken = default);
}
