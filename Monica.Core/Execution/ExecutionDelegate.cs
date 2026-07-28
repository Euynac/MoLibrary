namespace Monica.Core.Execution;

/// <summary>
/// Represents the next typed operation in an execution pipeline.
/// </summary>
/// <typeparam name="TResult">The execution result type.</typeparam>
/// <returns>The asynchronous execution result.</returns>
public delegate Task<TResult> ExecutionDelegate<TResult>();
