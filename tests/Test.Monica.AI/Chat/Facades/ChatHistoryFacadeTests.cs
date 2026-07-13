using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Monica.AI.Abstractions;
using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.Chat.Facades;
using Monica.AI.Chat.Models;
using Monica.AI.Chat.Providers;
using Monica.AI.Models;
using Monica.AI.Services;
using Monica.AI.Services.Support;
using Monica.Core.Results;
using Monica.Modules;
using NSubstitute;
using Test.Monica.AI.Support;

namespace Test.Monica.AI.Chat.Facades;

public sealed class ChatHistoryFacadeTests
{
    [Fact]
    public async Task GetCatalogAsync_WhenPersistenceIsDisabled_ShouldReturnEmptySuccessfulCatalog()
    {
        var facade = new ChatHistoryFacade(
            new NoOpChatHistoryProvider(),
            new NoOpChatHistoryPartitionResolver(),
            CreateChatService());

        var result = await facade.GetCatalogAsync(TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResStatus.Ok);
        result.Message.Should().BeNull();
        result.Data.Should().NotBeNull();
        result.Data!.Revision.Should().Be(0);
        result.Data.Sessions.Should().BeEmpty();
        result.Data.CurrentSessionId.Should().BeNull();
    }

    [Fact]
    public async Task SaveSessionAsync_WhenPersistenceIsDisabled_ShouldReturnStructuredFailureReason()
    {
        var chatService = CreateChatService();
        var facade = new ChatHistoryFacade(
            new NoOpChatHistoryProvider(),
            new NoOpChatHistoryPartitionResolver(),
            chatService);
        var now = DateTimeOffset.UtcNow;
        await using var session = chatService.RestoreSession(new ChatSessionSnapshot
        {
            SessionId = "session-1",
            Title = "Persisted chat",
            CreatedAt = now,
            UpdatedAt = now,
            Settings = new ChatSessionSettings("provider", "model", "prompt", false),
            Turns = []
        });

        var result = await facade.SaveSessionAsync(
            session,
            0,
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResStatus.Ok);
        result.Message.Should().BeNull();
        result.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be(ChatHistoryWriteStatus.NotPersisted);
        result.Data.FailureReason.Should().Be(ChatHistoryWriteFailureReason.PersistenceDisabled);
    }

    private static AIChatService CreateChatService() => new(
        Substitute.For<IAIProviderFactory>(),
        Options.Create(new ModuleAIOption()),
        new TestAIChatAgentFactory(static (_, _, _) =>
            throw new InvalidOperationException("Catalog reads must not activate an agent.")),
        Substitute.For<IAgentCapabilityStateStore>(),
        new AgentStreamingCoordinator(new AIChatRuntimeContextAccessor()));
}
