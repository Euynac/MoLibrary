using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Monica.AI.Abstractions;
using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.Chat.Models;
using Monica.AI.Models;
using Monica.AI.Services;
using Monica.AI.Services.Support;
using Monica.Modules;
using NSubstitute;
using Test.Monica.AI.Support;

namespace Test.Monica.AI.Chat.Models;

public sealed class ChatSessionSnapshotTests
{
    [Fact]
    public async Task CreateSnapshotAsync_WhenRestoredSessionIsLazy_ShouldRoundTripTranscriptWithoutApprovalState()
    {
        var service = CreateService();
        var original = CreateSnapshot();

        await using var session = service.RestoreSession(original);
        session.IsRuntimeActive.Should().BeFalse();

        var captured = await service.CreateSnapshotAsync(
            session,
            TestContext.Current.CancellationToken);
        var serialized = JsonSerializer.Serialize(captured);
        var roundTripped = JsonSerializer.Deserialize<ChatSessionSnapshot>(serialized);

        roundTripped.Should().NotBeNull();
        roundTripped!.SessionId.Should().Be(original.SessionId);
        roundTripped.Turns.Should().ContainSingle();
        roundTripped.Turns[0].HistoryCheckpoint.Should().Be(4);
        roundTripped.Turns[0].AssistantMessage!.ReasoningContent.Should().Be("reasoning");
        roundTripped.Turns[0].AssistantMessage!.ToolCalls.Should().ContainSingle()
            .Which.Arguments!.Value.GetProperty("path").GetString().Should().Be("readme.md");
        roundTripped.Turns[0].ErrorMessages.Should().ContainSingle()
            .Which.Content.Should().Be("retry failed");
        roundTripped.AgentSessionState!.Value.GetProperty("stateBag")
            .TryGetProperty("toolApprovalState", out _).Should().BeFalse();
        roundTripped.AgentSessionState!.Value.GetProperty("stateBag")
            .TryGetProperty("_autoApprovedFunctionCalls", out _).Should().BeFalse();
        serialized.Should().NotContain("pending-request");
        serialized.Should().NotContain("auto-approved-request");
        session.IsRuntimeActive.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WhenCallerMutatesCollections_ShouldRetainOwnedSnapshotData()
    {
        var requestUsages = new List<ChatRequestUsageSnapshot>
        {
            new(
                1,
                new DateTimeOffset(2026, 7, 13, 1, 0, 10, TimeSpan.Zero),
                "response-1",
                "message-1",
                new ChatTokenUsageSnapshot(1, 1, 0, 0, 2))
        };
        var toolCalls = new List<ChatToolCallSnapshot>
        {
            new()
            {
                ToolName = "read_file",
                CallId = "call-1",
                StartedAt = new DateTimeOffset(2026, 7, 13, 1, 0, 5, TimeSpan.Zero)
            }
        };
        var errors = new List<ChatMessageSnapshot>
        {
            CreateMessage("error-1", AIChatRole.Assistant, AIChatMessageKind.Error, "failed", 30)
        };
        var assistant = CreateMessage(
            "assistant-1",
            AIChatRole.Assistant,
            AIChatMessageKind.Message,
            "answer",
            20) with
        {
            RequestUsages = requestUsages,
            ToolCalls = toolCalls
        };
        var turns = new List<ChatTurnSnapshot>
        {
            new()
            {
                UserMessage = CreateMessage(
                    "user-1",
                    AIChatRole.User,
                    AIChatMessageKind.Message,
                    "question",
                    0),
                AssistantMessage = assistant,
                ErrorMessages = errors
            }
        };
        var snapshot = CreateSnapshot() with { Turns = turns };

        turns.Clear();
        errors.Clear();
        requestUsages.Clear();
        toolCalls.Clear();

        snapshot.Turns.Should().ContainSingle();
        snapshot.Turns[0].ErrorMessages.Should().ContainSingle();
        snapshot.Turns[0].AssistantMessage!.RequestUsages.Should().ContainSingle();
        snapshot.Turns[0].AssistantMessage!.ToolCalls.Should().ContainSingle();
    }

    [Fact]
    public void Constructor_WhenSourceJsonDocumentsAreDisposed_ShouldRetainOwnedJsonValues()
    {
        ChatSessionSnapshot snapshot;
        using (var agentState = JsonDocument.Parse("""{"stateBag":{"value":42}}"""))
        using (var arguments = JsonDocument.Parse("""{"path":"owned.md"}"""))
        {
            var toolCall = new ChatToolCallSnapshot
            {
                ToolName = "read_file",
                CallId = "call-1",
                Arguments = arguments.RootElement,
                StartedAt = new DateTimeOffset(2026, 7, 13, 1, 0, 5, TimeSpan.Zero)
            };
            var original = CreateSnapshot();
            var assistant = original.Turns[0].AssistantMessage! with { ToolCalls = [toolCall] };
            snapshot = original with
            {
                AgentSessionState = agentState.RootElement,
                Turns = [original.Turns[0] with { AssistantMessage = assistant }]
            };
        }

        snapshot.AgentSessionState!.Value.GetProperty("stateBag").GetProperty("value")
            .GetInt32().Should().Be(42);
        snapshot.Turns[0].AssistantMessage!.ToolCalls[0].Arguments!.Value
            .GetProperty("path").GetString().Should().Be("owned.md");
    }

    private static AIChatService CreateService()
    {
        var capabilityStateStore = Substitute.For<IAgentCapabilityStateStore>();
        var agentFactory = new TestAIChatAgentFactory(static (_, _, _) =>
            throw new InvalidOperationException("A lazy snapshot must not activate the runtime."));
        return new AIChatService(
            Substitute.For<IAIProviderFactory>(),
            Options.Create(new ModuleAIOption()),
            agentFactory,
            capabilityStateStore,
            new AgentStreamingCoordinator(
                new AIChatRuntimeContextAccessor(),
                new AgentResponseUpdateChannelContext()));
    }

    private static ChatSessionSnapshot CreateSnapshot() => new()
    {
        SessionId = "session-1",
        Title = "Persisted chat",
        CreatedAt = new DateTimeOffset(2026, 7, 13, 1, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 7, 13, 1, 1, 0, TimeSpan.Zero),
        Settings = new ChatSessionSettings("provider", "model", "prompt", true),
        AgentHistoryMessageCount = 7,
        Revision = 5,
        AgentSessionState = JsonSerializer.SerializeToElement(new
        {
            conversationId = (string?)null,
            stateBag = new Dictionary<string, object?>
            {
                ["InMemoryChatHistoryProvider"] = new { messages = Array.Empty<object>() },
                ["toolApprovalState"] = new
                {
                    rules = Array.Empty<object>(),
                    queuedApprovalRequests = new[] { "pending-request" }
                },
                ["_autoApprovedFunctionCalls"] = new[] { "auto-approved-request" }
            }
        }),
        Turns =
        [
            new ChatTurnSnapshot
            {
                HistoryCheckpoint = 4,
                UserMessage = new ChatMessageSnapshot
                {
                    Id = "user-1",
                    Role = AIChatRole.User,
                    Kind = AIChatMessageKind.Message,
                    Content = "question",
                    CreatedAt = new DateTimeOffset(2026, 7, 13, 1, 0, 0, TimeSpan.Zero)
                },
                AssistantMessage = new ChatMessageSnapshot
                {
                    Id = "assistant-1",
                    Role = AIChatRole.Assistant,
                    Kind = AIChatMessageKind.Message,
                    Content = "answer",
                    CreatedAt = new DateTimeOffset(2026, 7, 13, 1, 0, 30, TimeSpan.Zero),
                    ProviderId = "provider",
                    ModelName = "model",
                    ReasoningContent = "reasoning",
                    ReasoningDurationSeconds = 1.5,
                    Usage = new ChatTokenUsageSnapshot(10, 4, 2, 3, 14),
                    RequestUsages =
                    [
                        new ChatRequestUsageSnapshot(
                            1,
                            new DateTimeOffset(2026, 7, 13, 1, 0, 20, TimeSpan.Zero),
                            "response-1",
                            "message-1",
                            new ChatTokenUsageSnapshot(10, 4, 2, 3, 14))
                    ],
                    ToolCalls =
                    [
                        new ChatToolCallSnapshot
                        {
                            ToolName = "read_file",
                            CallId = "call-1",
                            Arguments = JsonSerializer.SerializeToElement(new { path = "readme.md" }),
                            ArgumentsText = "{\"path\":\"readme.md\"}",
                            ResultText = "content",
                            Status = ToolCallStatus.Completed,
                            StartedAt = new DateTimeOffset(2026, 7, 13, 1, 0, 5, TimeSpan.Zero),
                            CompletedAt = new DateTimeOffset(2026, 7, 13, 1, 0, 6, TimeSpan.Zero)
                        }
                    ]
                },
                ErrorMessages =
                [
                    new ChatMessageSnapshot
                    {
                        Id = "error-1",
                        Role = AIChatRole.Assistant,
                        Kind = AIChatMessageKind.Error,
                        Content = "retry failed",
                        CreatedAt = new DateTimeOffset(2026, 7, 13, 1, 1, 0, TimeSpan.Zero)
                    }
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
}
