using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Monica.AI.Abstractions;

namespace Monica.AI.Services.Support;

/// <summary>
/// Emits synthetic streaming updates for function calls and results so the UI can observe tool activity.
/// </summary>
public sealed class ToolInvocationTrackingAgentDecorator(
    ILogger<ToolInvocationTrackingAgentDecorator> logger)
    : IAIChatAgentDecorator
{
    /// <inheritdoc />
    public void Configure(AIAgentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        logger.LogInformation("Configuring tool invocation tracking middleware.");
        builder.Use(TrackInvocationAsync);
    }

    private async ValueTask<object?> TrackInvocationAsync(
        AIAgent _,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var updateChannel = ResolveUpdateChannel(context.Options?.AdditionalProperties);
        var functionCall = CreateFunctionCallContent(context);

        logger.LogInformation(
            "Observed tool invocation '{ToolName}' (CallId: {CallId}). Channel available: {HasChannel}.",
            functionCall.Name,
            functionCall.CallId,
            updateChannel is not null);

        if (updateChannel is not null)
        {
            await updateChannel.PublishAsync(
                new AgentResponseUpdate(ChatRole.Assistant, [functionCall])
                {
                    CreatedAt = DateTimeOffset.UtcNow
                },
                cancellationToken);
        }

        try
        {
            var result = await next(context, cancellationToken).ConfigureAwait(false);

            if (updateChannel is not null)
            {
                await updateChannel.PublishAsync(
                    CreateFunctionResultUpdate(functionCall.CallId, result, exception: null),
                    cancellationToken);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Tool invocation '{ToolName}' failed and will be returned to the model as a tool error result.",
                functionCall.Name);

            var errorResult = ToolInvocationErrorResult.Create(functionCall, ex);
            if (updateChannel is not null)
            {
                await updateChannel.PublishAsync(
                    CreateFunctionResultUpdate(functionCall.CallId, errorResult, ex),
                    cancellationToken);
            }

            return errorResult;
        }
    }

    private static AgentResponseUpdate CreateFunctionResultUpdate(
        string callId,
        object? result,
        Exception? exception)
    {
        var content = new FunctionResultContent(callId, result)
        {
            Exception = exception
        };

        return new AgentResponseUpdate(ChatRole.Tool, [content])
        {
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static FunctionCallContent CreateFunctionCallContent(FunctionInvocationContext context)
    {
        var original = context.CallContent;
        var callId = !string.IsNullOrWhiteSpace(original?.CallId)
            ? original.CallId
            : Guid.NewGuid().ToString("N");
        var name = !string.IsNullOrWhiteSpace(original?.Name)
            ? original.Name
            : context.Function?.Name ?? "unknown_tool";
        var arguments = original?.Arguments is { Count: > 0 }
            ? new Dictionary<string, object?>(original.Arguments)
            : new Dictionary<string, object?>(context.Arguments ?? []);

        return new FunctionCallContent(callId, name, arguments);
    }

    private static AgentResponseUpdateChannel? ResolveUpdateChannel(
        AdditionalPropertiesDictionary? additionalProperties)
    {
        if (additionalProperties is not null
            && additionalProperties.TryGetValue<AgentResponseUpdateChannel>(out var updateChannel))
        {
            return updateChannel;
        }

        return AgentResponseUpdateChannelContext.Current;
    }
}
