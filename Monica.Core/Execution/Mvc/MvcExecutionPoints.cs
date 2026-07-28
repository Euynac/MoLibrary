using Monica.Core.Execution;

namespace Monica.Core.Execution.Mvc;

/// <summary>
/// Stable execution points emitted by Monica's MVC execution adapter.
/// </summary>
public static class MvcExecutionPoints
{
    /// <summary>
    /// Represents a direct MVC action that does not dispatch through another Monica execution adapter.
    /// </summary>
    public static readonly ExecutionPoint Action = new("webapi.mvc-action");
}
