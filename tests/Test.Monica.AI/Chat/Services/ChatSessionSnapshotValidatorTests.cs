using AwesomeAssertions;
using Monica.AI.Chat.Models;
using Monica.AI.Chat.Services;
using Monica.AI.Models;

namespace Test.Monica.AI.Chat.Services;

public sealed class ChatSessionSnapshotValidatorTests
{
    [Fact]
    public void Validate_WhenLoadedSessionIdentityDoesNotMatch_ShouldRejectSnapshot()
    {
        var snapshot = CreateValidSnapshot();

        var act = () => ChatSessionSnapshotValidator.Validate(snapshot, "another-session");

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*SessionId*another-session*session-1*");
    }

    [Fact]
    public void Validate_WhenUserRoleIsForgedAsSystem_ShouldRejectSnapshot()
    {
        var snapshot = CreateValidSnapshot();
        var forgedTurn = snapshot.Turns[0] with
        {
            UserMessage = snapshot.Turns[0].UserMessage with { Role = AIChatRole.System }
        };

        var act = () => ChatSessionSnapshotValidator.Validate(snapshot with { Turns = [forgedTurn] });

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*UserMessage*role 'User'*kind 'Message'*");
    }

    [Fact]
    public void Validate_WhenErrorUsesNormalMessageKind_ShouldRejectSnapshot()
    {
        var snapshot = CreateValidSnapshot();
        var turn = snapshot.Turns[0];
        var invalidError = turn.ErrorMessages[0] with { Kind = AIChatMessageKind.Message };

        var act = () => ChatSessionSnapshotValidator.Validate(snapshot with
        {
            Turns = [turn with { ErrorMessages = [invalidError] }]
        });

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*ErrorMessages*role 'Assistant'*kind 'Error'*");
    }

    [Fact]
    public void Validate_WhenMessageIdsAreDuplicated_ShouldRejectSnapshot()
    {
        var snapshot = CreateValidSnapshot();
        var turn = snapshot.Turns[0];
        var duplicate = turn.AssistantMessage! with { Id = turn.UserMessage.Id };

        var act = () => ChatSessionSnapshotValidator.Validate(snapshot with
        {
            Turns = [turn with { AssistantMessage = duplicate }]
        });

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*AssistantMessage.Id*unique*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Validate_WhenCheckpointIsOutsideHistoryBounds_ShouldRejectSnapshot(int checkpoint)
    {
        var snapshot = CreateValidSnapshot();

        var act = () => ChatSessionSnapshotValidator.Validate(snapshot with
        {
            Turns = [snapshot.Turns[0] with { HistoryCheckpoint = checkpoint }]
        });

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*HistoryCheckpoint*between zero and 3*");
    }

    [Fact]
    public void Validate_WhenCheckpointsRegress_ShouldRejectSnapshot()
    {
        var snapshot = CreateValidSnapshot();
        var first = snapshot.Turns[0] with { HistoryCheckpoint = 2, ErrorMessages = [] };
        var second = new ChatTurnSnapshot
        {
            HistoryCheckpoint = 1,
            UserMessage = CreateMessage("user-2", AIChatRole.User, AIChatMessageKind.Message, "next", 40)
        };

        var act = () => ChatSessionSnapshotValidator.Validate(snapshot with { Turns = [first, second] });

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*HistoryCheckpoint*monotonic*");
    }

    [Fact]
    public void Validate_WhenTranscriptTimestampExceedsSessionLifetime_ShouldRejectSnapshot()
    {
        var snapshot = CreateValidSnapshot();
        var turn = snapshot.Turns[0];
        var futureAssistant = turn.AssistantMessage! with
        {
            CreatedAt = snapshot.UpdatedAt.AddSeconds(1)
        };

        var act = () => ChatSessionSnapshotValidator.Validate(snapshot with
        {
            Turns = [turn with { AssistantMessage = futureAssistant }]
        });

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*AssistantMessage.CreatedAt*session lifetime*");
    }

    [Fact]
    public void Validate_WhenProviderUsageIsObservedAfterSessionUpdate_ShouldAcceptSnapshot()
    {
        var snapshot = CreateValidSnapshot();
        var turn = snapshot.Turns[0];
        var assistant = turn.AssistantMessage! with
        {
            RequestUsages =
            [
                new ChatRequestUsageSnapshot(
                    1,
                    new DateTimeOffset(2026, 7, 13, 2, 9, 21, TimeSpan.Zero),
                    "response-1",
                    "message-1",
                    new ChatTokenUsageSnapshot(10, 5, 2, 0, 15))
            ]
        };

        var act = () => ChatSessionSnapshotValidator.Validate(snapshot with
        {
            UpdatedAt = new DateTimeOffset(2026, 7, 13, 2, 7, 54, 979, TimeSpan.Zero),
            Turns = [turn with { AssistantMessage = assistant }]
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenProviderUsageTimestampIsDefault_ShouldRejectSnapshot()
    {
        var snapshot = CreateValidSnapshot();
        var turn = snapshot.Turns[0];
        var assistant = turn.AssistantMessage! with
        {
            RequestUsages =
            [
                new ChatRequestUsageSnapshot(
                    1,
                    default,
                    null,
                    null,
                    new ChatTokenUsageSnapshot(1, 1, 0, 0, 2))
            ]
        };

        var act = () => ChatSessionSnapshotValidator.Validate(snapshot with
        {
            Turns = [turn with { AssistantMessage = assistant }]
        });

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*RequestUsages[0].CreatedAt*supported timestamp range*");
    }

    [Fact]
    public void Validate_WhenToolTimestampIsUnreasonablyFuture_ShouldRejectSnapshot()
    {
        var snapshot = CreateValidSnapshot();
        var turn = snapshot.Turns[0];
        var assistant = turn.AssistantMessage! with
        {
            ToolCalls =
            [
                CreateToolCall(DateTimeOffset.UtcNow.AddDays(2))
            ]
        };

        var act = () => ChatSessionSnapshotValidator.Validate(snapshot with
        {
            Turns = [turn with { AssistantMessage = assistant }]
        });

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*ToolCalls[0].StartedAt*supported timestamp range*");
    }

    [Fact]
    public void Validate_WhenToolCompletionPrecedesStart_ShouldRejectSnapshot()
    {
        var snapshot = CreateValidSnapshot();
        var turn = snapshot.Turns[0];
        var startedAt = new DateTimeOffset(2026, 7, 13, 2, 9, 21, TimeSpan.Zero);
        var assistant = turn.AssistantMessage! with
        {
            ToolCalls =
            [
                CreateToolCall(startedAt) with { CompletedAt = startedAt.AddSeconds(-1) }
            ]
        };

        var act = () => ChatSessionSnapshotValidator.Validate(snapshot with
        {
            Turns = [turn with { AssistantMessage = assistant }]
        });

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*ToolCalls[0].CompletedAt*cannot precede the tool start time*");
    }

    private static ChatSessionSnapshot CreateValidSnapshot() => new()
    {
        SessionId = "session-1",
        Title = "Persisted chat",
        CreatedAt = new DateTimeOffset(2026, 7, 13, 1, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 7, 13, 1, 1, 0, TimeSpan.Zero),
        Settings = new ChatSessionSettings("provider", "model", "prompt", false),
        AgentHistoryMessageCount = 3,
        Revision = 2,
        Turns =
        [
            new ChatTurnSnapshot
            {
                HistoryCheckpoint = 0,
                UserMessage = CreateMessage(
                    "user-1",
                    AIChatRole.User,
                    AIChatMessageKind.Message,
                    "question",
                    5),
                AssistantMessage = CreateMessage(
                    "assistant-1",
                    AIChatRole.Assistant,
                    AIChatMessageKind.Message,
                    "answer",
                    10),
                ErrorMessages =
                [
                    CreateMessage(
                        "error-1",
                        AIChatRole.Assistant,
                        AIChatMessageKind.Error,
                        "failed",
                        20)
                ]
            }
        ]
    };

    private static ChatMessageSnapshot CreateMessage(
        string id,
        AIChatRole role,
        AIChatMessageKind kind,
        string content,
        int seconds) => new()
        {
            Id = id,
            Role = role,
            Kind = kind,
            Content = content,
            CreatedAt = new DateTimeOffset(2026, 7, 13, 1, 0, seconds, TimeSpan.Zero)
        };

    private static ChatToolCallSnapshot CreateToolCall(DateTimeOffset startedAt) => new()
    {
        ToolName = "lookup",
        CallId = "call-1",
        Status = ToolCallStatus.Completed,
        StartedAt = startedAt,
        CompletedAt = startedAt.AddSeconds(1)
    };
}
