using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Monica.WebApi.AutoControllers.Models;

/// <summary>
/// Represents either an executed MVC action or an execution-pipeline short circuit.
/// </summary>
public sealed class MvcActionExecutionResult
{
    private MvcActionExecutionResult(
        IActionResult? result,
        ActionExecutedContext? executedContext)
    {
        Result = result;
        ExecutedContext = executedContext;
    }

    /// <summary>
    /// Gets the MVC result produced by the action or a short-circuiting behavior.
    /// </summary>
    public IActionResult? Result { get; }

    /// <summary>
    /// Gets the completed MVC context when the controller action ran, or <see langword="null"/> when a behavior
    /// short-circuited before action execution.
    /// </summary>
    public ActionExecutedContext? ExecutedContext { get; }

    /// <summary>
    /// Creates a result for a controller action that MVC executed.
    /// </summary>
    public static MvcActionExecutionResult FromExecutedAction(ActionExecutedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new MvcActionExecutionResult(context.Result, context);
    }

    /// <summary>
    /// Creates a result that skips controller-action execution and returns the supplied MVC result.
    /// </summary>
    public static MvcActionExecutionResult ShortCircuit(IActionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new MvcActionExecutionResult(result, executedContext: null);
    }
}
