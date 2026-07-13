using Monica.AI.Chat.Models;
using Monica.AI.Models;

namespace Monica.AI.Chat.Services;

internal static class ChatSessionSnapshotValidator
{
    private static readonly DateTimeOffset MINIMUM_TIMESTAMP = DateTimeOffset.UnixEpoch;
    private static readonly TimeSpan MAXIMUM_FUTURE_SKEW = TimeSpan.FromDays(1);

    public static void Validate(ChatSessionSnapshot snapshot, string? expectedSessionId = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Version != ChatSessionSnapshot.CurrentVersion)
        {
            throw new NotSupportedException(
                $"Chat session snapshot version '{snapshot.Version}' is not supported.");
        }

        ValidateIdentifier(snapshot.SessionId, nameof(snapshot.SessionId));
        if (expectedSessionId is not null
            && !string.Equals(snapshot.SessionId, expectedSessionId, StringComparison.Ordinal))
        {
            throw Invalid(
                nameof(snapshot.SessionId),
                $"expected '{expectedSessionId}' but found '{snapshot.SessionId}'.");
        }

        if (snapshot.Revision < 0)
        {
            throw Invalid(nameof(snapshot.Revision), "cannot be negative.");
        }

        if (snapshot.AgentHistoryMessageCount < 0)
        {
            throw Invalid(nameof(snapshot.AgentHistoryMessageCount), "cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.Title))
        {
            throw Invalid(nameof(snapshot.Title), "cannot be empty.");
        }

        if (snapshot.Settings is null || string.IsNullOrWhiteSpace(snapshot.Settings.ProviderId))
        {
            throw Invalid(nameof(snapshot.Settings), "must identify a provider.");
        }

        ValidateTimestamp(snapshot.CreatedAt, nameof(snapshot.CreatedAt));
        ValidateTimestamp(snapshot.UpdatedAt, nameof(snapshot.UpdatedAt));
        if (snapshot.UpdatedAt < snapshot.CreatedAt)
        {
            throw Invalid(nameof(snapshot.UpdatedAt), "cannot precede the session creation time.");
        }

        ValidateTurns(snapshot);
    }

    private static void ValidateTurns(ChatSessionSnapshot snapshot)
    {
        var messageIds = new HashSet<string>(StringComparer.Ordinal);
        var previousCheckpoint = -1;
        var previousMessageTimestamp = snapshot.CreatedAt;

        for (var turnIndex = 0; turnIndex < snapshot.Turns.Count; turnIndex++)
        {
            var turn = snapshot.Turns[turnIndex]
                       ?? throw Invalid($"Turns[{turnIndex}]", "cannot be null.");
            var turnPath = $"Turns[{turnIndex}]";

            if (turn.HistoryCheckpoint < 0
                || turn.HistoryCheckpoint > snapshot.AgentHistoryMessageCount)
            {
                throw Invalid(
                    $"{turnPath}.{nameof(turn.HistoryCheckpoint)}",
                    $"must be between zero and {snapshot.AgentHistoryMessageCount}.");
            }

            if (turn.HistoryCheckpoint < previousCheckpoint)
            {
                throw Invalid(
                    $"{turnPath}.{nameof(turn.HistoryCheckpoint)}",
                    "must be monotonic across turns.");
            }

            previousCheckpoint = turn.HistoryCheckpoint;
            previousMessageTimestamp = ValidateMessage(
                turn.UserMessage,
                AIChatRole.User,
                AIChatMessageKind.Message,
                $"{turnPath}.{nameof(turn.UserMessage)}",
                snapshot,
                previousMessageTimestamp,
                messageIds);

            if (turn.AssistantMessage is not null)
            {
                previousMessageTimestamp = ValidateMessage(
                    turn.AssistantMessage,
                    AIChatRole.Assistant,
                    AIChatMessageKind.Message,
                    $"{turnPath}.{nameof(turn.AssistantMessage)}",
                    snapshot,
                    previousMessageTimestamp,
                    messageIds);
            }

            for (var errorIndex = 0; errorIndex < turn.ErrorMessages.Count; errorIndex++)
            {
                previousMessageTimestamp = ValidateMessage(
                    turn.ErrorMessages[errorIndex],
                    AIChatRole.Assistant,
                    AIChatMessageKind.Error,
                    $"{turnPath}.{nameof(turn.ErrorMessages)}[{errorIndex}]",
                    snapshot,
                    previousMessageTimestamp,
                    messageIds);
            }
        }
    }

    private static DateTimeOffset ValidateMessage(
        ChatMessageSnapshot message,
        AIChatRole expectedRole,
        AIChatMessageKind expectedKind,
        string path,
        ChatSessionSnapshot session,
        DateTimeOffset previousTimestamp,
        HashSet<string> messageIds)
    {
        if (message is null)
        {
            throw Invalid(path, "cannot be null.");
        }

        ValidateIdentifier(message.Id, $"{path}.{nameof(message.Id)}");
        if (!messageIds.Add(message.Id))
        {
            throw Invalid($"{path}.{nameof(message.Id)}", "must be unique within the session.");
        }

        if (message.Role != expectedRole || message.Kind != expectedKind)
        {
            throw Invalid(
                path,
                $"must use role '{expectedRole}' and kind '{expectedKind}'.");
        }

        if (message.Content is null)
        {
            throw Invalid($"{path}.{nameof(message.Content)}", "cannot be null.");
        }

        ValidateTimestamp(message.CreatedAt, $"{path}.{nameof(message.CreatedAt)}");
        if (message.CreatedAt < session.CreatedAt
            || message.CreatedAt > session.UpdatedAt)
        {
            throw Invalid(
                $"{path}.{nameof(message.CreatedAt)}",
                "must be within the session lifetime.");
        }

        if (message.CreatedAt < previousTimestamp)
        {
            throw Invalid(
                $"{path}.{nameof(message.CreatedAt)}",
                "must be chronological within the transcript.");
        }

        ValidateNestedTimestamps(message, path);

        return message.CreatedAt;
    }

    private static void ValidateNestedTimestamps(
        ChatMessageSnapshot message,
        string path)
    {
        for (var usageIndex = 0; usageIndex < message.RequestUsages.Count; usageIndex++)
        {
            var usage = message.RequestUsages[usageIndex];
            ValidateTimestamp(
                usage.CreatedAt,
                $"{path}.{nameof(message.RequestUsages)}[{usageIndex}].{nameof(usage.CreatedAt)}");
        }

        for (var toolIndex = 0; toolIndex < message.ToolCalls.Count; toolIndex++)
        {
            var toolCall = message.ToolCalls[toolIndex];
            var toolPath = $"{path}.{nameof(message.ToolCalls)}[{toolIndex}]";
            if (!Enum.IsDefined(toolCall.Status))
            {
                throw Invalid($"{toolPath}.{nameof(toolCall.Status)}", "is not supported.");
            }

            ValidateTimestamp(
                toolCall.StartedAt,
                $"{toolPath}.{nameof(toolCall.StartedAt)}");
            if (toolCall.CompletedAt is { } completedAt)
            {
                ValidateTimestamp(
                    completedAt,
                    $"{toolPath}.{nameof(toolCall.CompletedAt)}");
                if (completedAt < toolCall.StartedAt)
                {
                    throw Invalid(
                        $"{toolPath}.{nameof(toolCall.CompletedAt)}",
                        "cannot precede the tool start time.");
                }
            }
        }
    }

    private static void ValidateIdentifier(string? value, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(path, "cannot be empty.");
        }
    }

    private static void ValidateTimestamp(DateTimeOffset value, string path)
    {
        if (value < MINIMUM_TIMESTAMP || value > DateTimeOffset.UtcNow + MAXIMUM_FUTURE_SKEW)
        {
            throw Invalid(path, "is outside the supported timestamp range.");
        }
    }

    private static InvalidDataException Invalid(string path, string message)
        => new($"Invalid chat session snapshot at '{path}': {message}");
}
