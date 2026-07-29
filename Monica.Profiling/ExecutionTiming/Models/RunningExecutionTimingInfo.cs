namespace Monica.Profiling.ExecutionTiming.Models;

/// <summary>
/// Describes a currently running execution-timing operation.
/// </summary>
/// <param name="InvocationId">Unique identity of this concrete invocation.</param>
/// <param name="OperationKey">Stable machine identity used to aggregate equivalent operations.</param>
/// <param name="DisplayName">Human-readable operation name.</param>
/// <param name="StartedAt">Timestamp when the current sample started.</param>
/// <param name="Description">Optional display text supplied by the caller.</param>
public sealed record RunningExecutionTimingInfo(
    Guid InvocationId,
    string OperationKey,
    string DisplayName,
    DateTimeOffset StartedAt,
    string? Description)
{
    /// <summary>
    /// Returns the elapsed time, in milliseconds, from <see cref="StartedAt" /> to now.
    /// </summary>
    public long GetCurrentElapsedMs()
    {
        return (long)(DateTimeOffset.UtcNow - StartedAt).TotalMilliseconds;
    }
}
