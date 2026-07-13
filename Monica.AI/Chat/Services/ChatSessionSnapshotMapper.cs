using System.Text.Json;
using Monica.AI.Chat.Models;
using Monica.AI.Models;

namespace Monica.AI.Chat.Services;

internal static class ChatSessionSnapshotMapper
{
    public static ChatTurnSnapshot ToSnapshot(ChatTurn turn) => new()
    {
        UserMessage = ToSnapshot(turn.UserMessage),
        AssistantMessage = turn.AssistantMessage is null ? null : ToSnapshot(turn.AssistantMessage),
        ErrorMessages = turn.ErrorMessages.Select(ToSnapshot).ToArray(),
        HistoryCheckpoint = turn.HistoryCheckpoint
    };

    public static ChatTurn FromSnapshot(ChatTurnSnapshot snapshot)
    {
        var turn = new ChatTurn(FromSnapshot(snapshot.UserMessage), snapshot.HistoryCheckpoint)
        {
            AssistantMessage = snapshot.AssistantMessage is null
                ? null
                : FromSnapshot(snapshot.AssistantMessage)
        };
        turn.RestoreErrors(snapshot.ErrorMessages.Select(FromSnapshot));
        return turn;
    }

    private static ChatMessageSnapshot ToSnapshot(AIChatMessage message) => new()
    {
        Id = message.Id,
        Role = message.Role,
        Kind = message.Kind,
        Content = message.Content,
        CreatedAt = message.CreatedAt,
        ModelName = message.ModelName,
        ProviderId = message.ProviderId,
        Usage = message.Usage is null ? null : ToSnapshot(message.Usage),
        RequestUsages = message.RequestUsages?.Select(ToSnapshot).ToArray() ?? [],
        ReasoningContent = message.ReasoningContent,
        ReasoningDurationSeconds = message.ReasoningDurationSeconds,
        ToolCalls = message.ToolCalls?.Select(ToSnapshot).ToArray() ?? []
    };

    private static AIChatMessage FromSnapshot(ChatMessageSnapshot message) => new()
    {
        Id = message.Id,
        Role = message.Role,
        Kind = message.Kind,
        Content = message.Content,
        CreatedAt = message.CreatedAt,
        ModelName = message.ModelName,
        ProviderId = message.ProviderId,
        Usage = message.Usage is null ? null : FromSnapshot(message.Usage),
        RequestUsages = message.RequestUsages.Select(FromSnapshot).ToList(),
        ReasoningContent = message.ReasoningContent,
        ReasoningDurationSeconds = message.ReasoningDurationSeconds,
        ToolCalls = message.ToolCalls.Select(FromSnapshot).ToList()
    };

    private static ChatTokenUsageSnapshot ToSnapshot(TokenUsage usage) => new(
        usage.InputTokens,
        usage.OutputTokens,
        usage.ReasoningTokens,
        usage.CachedInputTokens,
        usage.TotalTokens);

    private static TokenUsage FromSnapshot(ChatTokenUsageSnapshot usage) => new()
    {
        InputTokens = usage.InputTokens,
        OutputTokens = usage.OutputTokens,
        ReasoningTokens = usage.ReasoningTokens,
        CachedInputTokens = usage.CachedInputTokens,
        TotalTokens = usage.TotalTokens
    };

    private static ChatRequestUsageSnapshot ToSnapshot(AIChatRequestUsage usage) => new(
        usage.Sequence,
        usage.CreatedAt,
        usage.ResponseId,
        usage.MessageId,
        ToSnapshot(usage.Usage));

    private static AIChatRequestUsage FromSnapshot(ChatRequestUsageSnapshot usage) => new()
    {
        Sequence = usage.Sequence,
        CreatedAt = usage.CreatedAt,
        ResponseId = usage.ResponseId,
        MessageId = usage.MessageId,
        Usage = FromSnapshot(usage.Usage)
    };

    private static ChatToolCallSnapshot ToSnapshot(ToolCallInfo toolCall) => new()
    {
        ToolName = toolCall.ToolName,
        CallId = toolCall.CallId,
        Arguments = SerializeArguments(toolCall.Arguments),
        ArgumentsText = toolCall.ArgumentsText,
        ResultText = toolCall.ResultText,
        ExceptionMessage = toolCall.ExceptionMessage,
        Status = toolCall.Status,
        StartedAt = toolCall.StartedAt,
        CompletedAt = toolCall.CompletedAt
    };

    private static ToolCallInfo FromSnapshot(ChatToolCallSnapshot toolCall) => new()
    {
        ToolName = toolCall.ToolName,
        CallId = toolCall.CallId,
        Arguments = DeserializeArguments(toolCall.Arguments),
        ArgumentsText = toolCall.ArgumentsText,
        ResultText = toolCall.ResultText,
        ExceptionMessage = toolCall.ExceptionMessage,
        Status = toolCall.Status,
        StartedAt = toolCall.StartedAt,
        CompletedAt = toolCall.CompletedAt
    };

    private static JsonElement? SerializeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.SerializeToElement(arguments);
        }
        catch (NotSupportedException)
        {
            // ArgumentsText remains the authoritative display representation when a tool uses
            // runtime-only argument values that System.Text.Json cannot serialize.
            return null;
        }
    }

    private static IDictionary<string, object?>? DeserializeArguments(JsonElement? arguments)
    {
        if (arguments is not { ValueKind: JsonValueKind.Object } value)
        {
            return null;
        }

        return value.EnumerateObject().ToDictionary(
            static property => property.Name,
            static property => (object?)property.Value.Clone(),
            StringComparer.Ordinal);
    }
}
