using System.Runtime.CompilerServices;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Monica.AI.Abstractions;
using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.Chat.Models;
using Monica.AI.Models;
using Monica.AI.Models.Internal;
using Monica.AI.Services;
using Monica.AI.Services.Support;
using Monica.Modules;
using NSubstitute;
using Test.Monica.AI.Support;

namespace Test.Monica.AI.Services;

public sealed class AIChatServicePersistenceTests
{
    [Fact]
    public async Task SendMessageAsync_WhenAgentSessionStateIsValid_ShouldContinueDeserializedHistory()
    {
        using var chatClient = new CapturingChatClient();
        var sourceAgent = new ChatClientAgent(chatClient);
        var sourceSession = await sourceAgent.CreateSessionAsync(TestContext.Current.CancellationToken);
        sourceAgent.GetService<InMemoryChatHistoryProvider>()!.SetMessages(
            sourceSession,
            [
                new ChatMessage(ChatRole.User, "first question"),
                new ChatMessage(ChatRole.Assistant, "first answer")
            ]);
        var serializedState = await sourceAgent.SerializeSessionAsync(
            sourceSession,
            cancellationToken: TestContext.Current.CancellationToken);
        var service = CreateService(chatClient);
        var snapshot = CreateInvalidStateSnapshot() with
        {
            AgentSessionState = serializedState,
            AgentHistoryMessageCount = 2
        };

        await using var session = service.RestoreSession(snapshot);
        var result = await service.SendMessageAsync(
            session,
            "second question",
            TestContext.Current.CancellationToken);

        result.Should().Be("continued answer");
        session.RestorationState.Should().Be(ChatSessionRestorationState.Restored);
        chatClient.CapturedMessages.Select(static message => message.Text)
            .Should().Equal("first question", "first answer", "second question");
    }

    [Fact]
    public async Task SendMessageAsync_WhenSerializedStateIsInvalid_ShouldContinueFromVisibleTranscript()
    {
        using var chatClient = new CapturingChatClient();
        var service = CreateService(chatClient);
        var snapshot = CreateInvalidStateSnapshot();

        await using var session = service.RestoreSession(snapshot);
        var result = await service.SendMessageAsync(
            session,
            "second question",
            TestContext.Current.CancellationToken);

        result.Should().Be("continued answer");
        session.RestorationState.Should().Be(ChatSessionRestorationState.TranscriptFallback);
        session.IsRuntimeActive.Should().BeTrue();
        chatClient.CapturedMessages.Select(static message => message.Text)
            .Should().Equal("first question", "first answer", "second question");
    }

    [Fact]
    public async Task RewindForRetry_WhenFallbackRebasesToolRichCheckpoint_ShouldRemoveTheRestoredTurn()
    {
        using var chatClient = new CapturingChatClient();
        var service = CreateService(chatClient);
        var snapshot = CreateInvalidStateSnapshot(historyCheckpoint: 8);

        await using var session = service.RestoreSession(snapshot);
        await service.ActivateSessionAsync(session, TestContext.Current.CancellationToken);
        var retryContent = session.RewindForRetry("assistant-1");

        retryContent.Should().Be("first question");
        session.Turns.Should().BeEmpty();
        session.ChatHistory.Should().BeEmpty();
    }

    private static AIChatService CreateService(CapturingChatClient chatClient)
    {
        var provider = Substitute.For<IAIProvider>();
        provider.ProviderId.Returns("provider");
        provider.Info.Returns(new AIProviderInfo
        {
            ProviderId = "provider",
            ProviderType = "Test",
            DisplayName = "Test",
            IsValid = true
        });
        provider.GetChatClient(Arg.Any<string?>()).Returns(chatClient);

        var providerFactory = Substitute.For<IAIProviderFactory>();
        providerFactory.GetProvider("provider").Returns(provider);

        var capabilityStateStore = Substitute.For<IAgentCapabilityStateStore>();
        capabilityStateStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AgentCapabilityState { Revision = 3 });

        var agentFactory = new TestAIChatAgentFactory((_, _, _) =>
            Task.FromResult(new AIChatAgentRuntime(
                new ChatClientAgent(chatClient),
                [])));

        return new AIChatService(
            providerFactory,
            Options.Create(new ModuleAIOption()),
            agentFactory,
            capabilityStateStore,
            new AgentStreamingCoordinator(new AIChatRuntimeContextAccessor()));
    }

    private static ChatSessionSnapshot CreateInvalidStateSnapshot(int historyCheckpoint = 0) => new()
    {
        SessionId = "session-1",
        Title = "Restored",
        CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        UpdatedAt = DateTimeOffset.UtcNow,
        Settings = new ChatSessionSettings("provider", "model", "prompt", false),
        AgentSessionState = JsonSerializer.SerializeToElement("invalid-session-state"),
        AgentHistoryMessageCount = 10,
        Turns =
        [
            new ChatTurnSnapshot
            {
                HistoryCheckpoint = historyCheckpoint,
                UserMessage = new ChatMessageSnapshot
                {
                    Id = "user-1",
                    Role = AIChatRole.User,
                    Content = "first question",
                    CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-30)
                },
                AssistantMessage = new ChatMessageSnapshot
                {
                    Id = "assistant-1",
                    Role = AIChatRole.Assistant,
                    Content = "first answer",
                    CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-20)
                }
            }
        ]
    };

    private sealed class CapturingChatClient : IChatClient
    {
        internal IReadOnlyList<ChatMessage> CapturedMessages { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CapturedMessages = messages.ToList();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "continued answer")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            CapturedMessages = messages.ToList();
            yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextContent("continued answer")]);
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceType == typeof(IChatClient) ? this : null;

        public void Dispose()
        {
        }
    }
}
