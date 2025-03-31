using System.Collections.Concurrent;

namespace MoLibrary.BackgroundJob.Abstract.Jobs;

/// <summary>
///     Defines interface of a job manager.
/// </summary>
public interface IMoBackgroundJobManager
{
    public static readonly ConcurrentDictionary<Type, Type> JobTypeMap = new();

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
    static Task RegisterJob<TJobType, TArgs>() where TJobType : IMoBackgroundJob<TArgs>
    {
        return RegisterJob(typeof(TJobType), typeof(TArgs));
    }
    /// <summary>
    ///    Registers a job type to be executed
    /// </summary>
    static Task RegisterJob(Type jobType, Type argsType) 
    {
        JobTypeMap.AddOrUpdate(argsType, jobType, (_, _) => jobType);
        return Task.CompletedTask;
    }
}