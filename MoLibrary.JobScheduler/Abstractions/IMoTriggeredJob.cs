namespace MoLibrary.JobScheduler.Abstractions;

/// <summary>
///     Defines interface of a triggered job.
/// </summary>
public interface IMoTriggeredJob<in TArgs> : IMoJobDefinition
{
    /// <summary>
    ///     Executes the job with the <paramref name="args" />.
    /// </summary>
    /// <param name="args">Job arguments.</param>
    Task ExecuteAsync(TArgs args);
}