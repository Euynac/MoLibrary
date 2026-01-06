namespace MoLibrary.JobScheduler.Abstractions;

/// <summary>
///     Defines interface of a job manager.
/// </summary>
public interface IMoTriggeredJobManager
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
    ///     Cancels a scheduled job that has not yet started execution.
    /// </summary>
    /// <param name="instanceId">The instance ID returned from EnqueueAsync.</param>
    /// <returns>True if cancelled successfully, false if job not found or already started.</returns>
    Task<bool> CancelScheduledJobAsync(string instanceId);
}