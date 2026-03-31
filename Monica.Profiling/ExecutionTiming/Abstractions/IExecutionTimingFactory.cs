namespace Monica.Profiling.ExecutionTiming.Abstractions;

/// <summary>
/// Creates execution-timing recorders for one-shot scopes and reusable aggregate timing.
/// </summary>
public interface IExecutionTimingFactory
{
    /// <summary>
    /// Creates a reusable recorder that can collect multiple start/stop samples under the same name.
    /// </summary>
    /// <param name="name">Logical name of the measured operation.</param>
    /// <param name="description">Optional display text used in running-state views and logs.</param>
    /// <returns>A recorder that starts only when <see cref="IExecutionTimingRecorder.Start" /> is called.</returns>
    IExecutionTimingRecorder CreateRecorder(string name, string? description = null);

    /// <summary>
    /// Begins a timing scope immediately and stops it automatically when disposed.
    /// </summary>
    /// <param name="name">Logical name of the measured operation.</param>
    /// <param name="description">Optional display text used in running-state views and logs.</param>
    /// <returns>A disposable recorder that has already been started.</returns>
    IExecutionTimingRecorder BeginScope(string name, string? description = null);
}
