namespace Monica.Core.Execution;

/// <summary>
/// Executes a typed terminal operation through behaviors registered for its descriptor.
/// </summary>
/// <remarks>
/// Resolve the pipeline from the same dependency-injection scope as the target operation. Behavior instances are
/// resolved from that scope for every invocation; cached plans never retain behavior instances.
/// </remarks>
public interface IExecutionPipeline
{
    /// <summary>
    /// Executes a terminal operation through the applicable typed behaviors.
    /// </summary>
    /// <typeparam name="TInput">The execution input type.</typeparam>
    /// <typeparam name="TResult">The execution result type.</typeparam>
    /// <param name="context">The current execution context.</param>
    /// <param name="terminal">The subsystem-owned terminal operation.</param>
    /// <returns>The typed asynchronous result.</returns>
    Task<TResult> ExecuteAsync<TInput, TResult>(
        ExecutionContext<TInput> context,
        ExecutionDelegate<TResult> terminal);
}
