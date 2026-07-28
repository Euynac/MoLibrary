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
    /// <param name="descriptor">The reusable execution descriptor.</param>
    /// <param name="input">The invocation input.</param>
    /// <param name="target">The concrete target instance when one exists.</param>
    /// <param name="terminal">The subsystem-owned terminal operation.</param>
    /// <param name="cancellationToken">The cancellation token governing this invocation.</param>
    /// <param name="features">Optional adapter-specific invocation metadata.</param>
    /// <returns>The typed asynchronous result.</returns>
    Task<TResult> ExecuteAsync<TInput, TResult>(
        ExecutionDescriptor descriptor,
        TInput input,
        object? target,
        ExecutionDelegate<TResult> terminal,
        CancellationToken cancellationToken = default,
        ExecutionFeatureCollection? features = null);

    /// <summary>
    /// Executes a terminal operation without a result through the applicable typed behaviors.
    /// </summary>
    /// <typeparam name="TInput">The execution input type.</typeparam>
    /// <param name="descriptor">The reusable execution descriptor.</param>
    /// <param name="input">The invocation input.</param>
    /// <param name="target">The concrete target instance when one exists.</param>
    /// <param name="terminal">The subsystem-owned terminal operation.</param>
    /// <param name="cancellationToken">The cancellation token governing this invocation.</param>
    /// <param name="features">Optional adapter-specific invocation metadata.</param>
    /// <returns>A task that represents the complete execution.</returns>
    Task ExecuteAsync<TInput>(
        ExecutionDescriptor descriptor,
        TInput input,
        object? target,
        Func<Task> terminal,
        CancellationToken cancellationToken = default,
        ExecutionFeatureCollection? features = null);
}
