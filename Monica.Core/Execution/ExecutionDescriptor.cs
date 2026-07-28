using System.Reflection;

namespace Monica.Core.Execution;

/// <summary>
/// Describes one reusable execution boundary independently of a concrete invocation.
/// </summary>
/// <remarks>
/// Descriptors are immutable and may be cached by the execution pipeline. Do not place request-specific state in a
/// descriptor; use <see cref="ExecutionFeatureCollection"/> on the execution context instead.
/// </remarks>
public sealed record ExecutionDescriptor
{
    /// <summary>
    /// Initializes an execution descriptor.
    /// </summary>
    /// <param name="point">The subsystem execution point.</param>
    /// <param name="operationName">A stable operation name suitable for diagnostics.</param>
    /// <param name="componentType">The concrete or contractual component type being invoked.</param>
    /// <param name="entryMethod">The concrete entry method when one is available.</param>
    /// <param name="inputType">The pipeline input type.</param>
    /// <param name="resultType">The pipeline result type.</param>
    /// <param name="isBusinessOperation">Whether the execution represents application business work.</param>
    /// <param name="isLongRunning">
    /// Whether the execution may remain active for the lifetime of the host and therefore should not receive finite
    /// transaction or retry behaviors.
    /// </param>
    public ExecutionDescriptor(
        ExecutionPoint point,
        string operationName,
        Type componentType,
        MethodInfo? entryMethod,
        Type inputType,
        Type resultType,
        bool isBusinessOperation,
        bool isLongRunning)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(inputType);
        ArgumentNullException.ThrowIfNull(resultType);

        if (!string.Equals(operationName, operationName.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Execution operation names must not contain leading or trailing whitespace.",
                nameof(operationName));
        }

        Point = point;
        OperationName = operationName;
        ComponentType = componentType;
        EntryMethod = entryMethod;
        InputType = inputType;
        ResultType = resultType;
        IsBusinessOperation = isBusinessOperation;
        IsLongRunning = isLongRunning;
    }

    /// <summary>
    /// Gets the subsystem execution point.
    /// </summary>
    public ExecutionPoint Point { get; }

    /// <summary>
    /// Gets the stable operation name used for diagnostics and behavior selection.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets the concrete or contractual component type being invoked.
    /// </summary>
    public Type ComponentType { get; }

    /// <summary>
    /// Gets the concrete entry method when the adapter can identify one.
    /// </summary>
    public MethodInfo? EntryMethod { get; }

    /// <summary>
    /// Gets the pipeline input type.
    /// </summary>
    public Type InputType { get; }

    /// <summary>
    /// Gets the pipeline result type.
    /// </summary>
    public Type ResultType { get; }

    /// <summary>
    /// Gets whether the execution represents application business work.
    /// </summary>
    public bool IsBusinessOperation { get; }

    /// <summary>
    /// Gets whether the operation may remain active for the lifetime of the host.
    /// </summary>
    public bool IsLongRunning { get; }
}
