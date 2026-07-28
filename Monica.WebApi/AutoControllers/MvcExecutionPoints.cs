using Monica.Core.Execution;

namespace Monica.WebApi.AutoControllers;

/// <summary>
/// Stable execution points emitted by the MVC AutoControllers adapter.
/// </summary>
public static class MvcExecutionPoints
{
    /// <summary>
    /// Represents a direct MVC or generated CRUD action that does not dispatch through Mediator.
    /// </summary>
    public static readonly ExecutionPoint Action = new("webapi.mvc-action");
}
