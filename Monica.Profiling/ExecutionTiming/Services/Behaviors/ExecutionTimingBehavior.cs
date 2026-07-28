using Monica.Core.Execution;
using Monica.Profiling.ExecutionTiming.Abstractions;

namespace Monica.Profiling.ExecutionTiming.Services.Behaviors;

/// <summary>
/// Records the complete duration of each finite business execution.
/// </summary>
public sealed class ExecutionTimingBehavior<TInput, TResult>(IExecutionTimingFactory timingFactory)
    : IExecutionBehavior<TInput, TResult>
{
    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync(
        ExecutionContext<TInput> context,
        ExecutionDelegate<TResult> next)
    {
        var descriptor = context.Descriptor;
        using var timing = timingFactory.BeginInvocation(
            descriptor.OperationKey,
            descriptor.DisplayName,
            context.InvocationId,
            $"{descriptor.Point.Value}: {descriptor.ComponentType.FullName ?? descriptor.ComponentType.Name}");
        return await next();
    }
}
