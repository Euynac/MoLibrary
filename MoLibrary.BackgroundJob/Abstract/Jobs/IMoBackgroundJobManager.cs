namespace MoLibrary.BackgroundJob.Abstract.Jobs;

/// <summary>
///     Defines interface of a job manager.
/// </summary>
public interface IMoBackgroundJobManager
{
    /// <summary>
    ///     Enqueues a job to be executed.
    /// </summary>
    /// <typeparam name="TArgs">Type of the arguments of job.</typeparam>
    /// <param name="args">Job arguments.</param>
    /// <param name="priority">Job priority.</param>
    /// <param name="delay">Job delay (wait duration before first try).</param>
    /// <returns>Unique identifier of a background job.</returns>
    Task<string> EnqueueAsync<TArgs>(
        TArgs args,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        TimeSpan? delay = null
    );

    /// <summary>
    ///    Registers a job type to be executed
    /// </summary>
    /// <typeparam name="TJobType"></typeparam>
    /// <typeparam name="TArgs"></typeparam>
    /// <returns></returns>
    Task RegisterJob<TJobType, TArgs>() where TJobType : IMoBackgroundJob<TArgs>;
}