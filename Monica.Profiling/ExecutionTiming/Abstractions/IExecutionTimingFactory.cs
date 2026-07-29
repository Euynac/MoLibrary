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

    /// <summary>
    /// Begins timing one invocation of a stable operation identity.
    /// </summary>
    /// <param name="operationKey">Stable machine identity used to aggregate completed samples.</param>
    /// <param name="displayName">Human-readable operation name used by diagnostic views and logs.</param>
    /// <param name="invocationId">Unique identity of this concrete invocation.</param>
    /// <param name="description">Optional supplementary display text.</param>
    /// <returns>A disposable recorder that has already been started.</returns>
    IExecutionTimingRecorder BeginInvocation(
        string operationKey,
        string displayName,
        Guid invocationId,
        string? description = null);
}
