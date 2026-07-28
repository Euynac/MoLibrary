namespace Monica.Core.Execution;

/// <summary>
/// Carries the typed input and invocation-specific state for one pipeline execution.
/// </summary>
/// <typeparam name="TInput">The input type declared by the execution descriptor.</typeparam>
public sealed class ExecutionContext<TInput>
{
    /// <summary>
    /// Initializes an execution context.
    /// </summary>
    /// <param name="descriptor">The reusable execution descriptor.</param>
    /// <param name="input">The invocation input.</param>
    /// <param name="target">The concrete target instance when one exists.</param>
    /// <param name="cancellationToken">The cancellation token governing this invocation.</param>
    /// <param name="features">Optional adapter-specific features. A new collection is created when omitted.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the descriptor input type does not exactly match <typeparamref name="TInput"/>.
    /// </exception>
    public ExecutionContext(
        ExecutionDescriptor descriptor,
        TInput input,
        object? target = null,
        CancellationToken cancellationToken = default,
        ExecutionFeatureCollection? features = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.InputType != typeof(TInput))
        {
            throw new ArgumentException(
                $"Execution descriptor input type '{descriptor.InputType.FullName}' does not match context input type " +
                $"'{typeof(TInput).FullName}'.",
                nameof(descriptor));
        }

        Descriptor = descriptor;
        Input = input;
        Target = target;
        CancellationToken = cancellationToken;
        Features = features ?? new ExecutionFeatureCollection();
    }

    /// <summary>
    /// Gets the reusable execution descriptor.
    /// </summary>
    public ExecutionDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the invocation input.
    /// </summary>
    public TInput Input { get; }

    /// <summary>
    /// Gets the concrete target instance when the adapter has one.
    /// </summary>
    public object? Target { get; }

    /// <summary>
    /// Gets the cancellation token governing this invocation.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets adapter-specific invocation metadata.
    /// </summary>
    public ExecutionFeatureCollection Features { get; }
}
