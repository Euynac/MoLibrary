namespace Monica.Core.Execution;

/// <summary>
/// Adds a typed cross-cutting behavior around a Monica execution boundary.
/// </summary>
/// <typeparam name="TInput">The execution input type.</typeparam>
/// <typeparam name="TResult">The execution result type.</typeparam>
/// <remarks>
/// A behavior may short-circuit by returning without invoking the remaining pipeline. When it continues the pipeline,
/// it must invoke the supplied delegate at most once. Exceptions and cancellation should normally flow unchanged.
/// </remarks>
public interface IExecutionBehavior<TInput, TResult>
{
    /// <summary>
    /// Executes the behavior around the remaining pipeline.
    /// </summary>
    /// <param name="context">The current execution context.</param>
    /// <param name="next">The remaining pipeline, which may be invoked at most once.</param>
    /// <returns>The typed asynchronous result.</returns>
    Task<TResult> ExecuteAsync(ExecutionContext<TInput> context, ExecutionDelegate<TResult> next);
}
