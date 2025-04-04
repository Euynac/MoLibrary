﻿namespace MoLibrary.BackgroundJob.Abstract.Jobs;

/// <summary>
///     Defines interface of a background job.
/// </summary>
public interface IMoBackgroundJob<in TArgs>
{
    /// <summary>
    ///     Executes the job with the <paramref name="args" />.
    /// </summary>
    /// <param name="args">Job arguments.</param>
    Task ExecuteAsync(TArgs args);
}