using Microsoft.Extensions.AI;

namespace Monica.AI.Services.Support;

internal sealed record ToolInvocationErrorResult
{
    public string Status { get; init; } = "tool_error";

    public required string ToolName { get; init; }

    public required string CallId { get; init; }

    public required string ErrorType { get; init; }

    public required string Message { get; init; }

    public required string Instruction { get; init; }

    public IReadOnlyDictionary<string, object?>? Arguments { get; init; }

    public static ToolInvocationErrorResult Create(FunctionCallContent functionCall, Exception exception)
    {
        return new ToolInvocationErrorResult
        {
            ToolName = functionCall.Name,
            CallId = functionCall.CallId ?? string.Empty,
            ErrorType = exception.GetType().Name,
            Message = exception.Message,
            Instruction =
                "The tool call failed before it completed. Inspect the error and retry with corrected arguments when possible. Do not repeat the exact same call unchanged.",
            Arguments = functionCall.Arguments is { Count: > 0 }
                ? new Dictionary<string, object?>(functionCall.Arguments)
                : null
        };
    }

    public static ToolInvocationErrorResult Create(
        string toolName,
        IDictionary<string, object?>? arguments,
        Exception exception)
    {
        return new ToolInvocationErrorResult
        {
            ToolName = toolName,
            CallId = string.Empty,
            ErrorType = exception.GetType().Name,
            Message = exception.Message,
            Instruction =
                "The script call failed before it completed. Inspect the script schema and retry with corrected arguments. Do not repeat the exact same call unchanged.",
            Arguments = arguments is { Count: > 0 }
                ? new Dictionary<string, object?>(arguments)
                : null
        };
    }
}
