using Monica.Core.Execution;

namespace Monica.Core.Mediator;

/// <summary>
/// Stable execution points emitted by the Monica request mediator.
/// </summary>
public static class MediatorExecutionPoints
{
    /// <summary>
    /// Represents one request-handler invocation.
    /// </summary>
    public static readonly ExecutionPoint Request = new("mediator.request");
}
