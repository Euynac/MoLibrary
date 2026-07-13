using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Monica.AI.Chat.Abstractions;
using Monica.AI.Chat.Facades;
using Monica.AI.Chat.Models;
using Monica.AI.Models;
using Monica.AI.UI.UIChat.Models;
using Monica.AI.UI.UIChat.Providers.Browser;
using Monica.AI.UI.UIChat.State;
using Monica.Core.Results;
using Monica.Modules;
using Test.Monica.AI.UI.Support;

namespace Test.Monica.AI.UI.UIChat.State;

public sealed class ChatSessionWorkspaceTests
{
    private static readonly ChatHistoryPartition PARTITION = new("workspace-test");

    [Fact]
    public async Task InitializeAsync_WhenCurrentSnapshotExists_ShouldRestoreTranscriptWithoutRuntime()
    {
        await using var fixture = await WorkspaceFixture.CreateAsync();

        await fixture.Workspace.InitializeAsync(TestContext.Current.CancellationToken);

        fixture.Workspace.IsLoading.Should().BeFalse();
        fixture.Workspace.Sessions.Should().ContainSingle();
        fixture.Workspace.CurrentSessionId.Should().Be("session-1");
        fixture.Workspace.CurrentSession.Should().NotBeNull();
        fixture.Workspace.CurrentSession!.Messages.Should().ContainSingle(message => message.Content == "persisted message");
        fixture.Workspace.CurrentSession.IsRuntimeActive.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_WhenRevisionWasAdvancedElsewhere_ShouldKeepLocalHistoryAndWarn()
    {
        await using var fixture = await WorkspaceFixture.CreateAsync();
        await fixture.Workspace.InitializeAsync(TestContext.Current.CancellationToken);
        var warnings = new List<ChatHistoryWorkspaceWarning>();
        fixture.Workspace.WarningRaised += warnings.Add;
        _ = await fixture.Provider.SetCurrentSessionAsync(
            PARTITION,
            "session-1",
            fixture.Workspace.Revision,
            TestContext.Current.CancellationToken);

        var cleared = await fixture.Workspace.ClearAsync(TestContext.Current.CancellationToken);

        cleared.Should().BeFalse();
        fixture.Workspace.Sessions.Should().ContainSingle();
        warnings.Should().ContainSingle(item => item.Kind == ChatHistoryWorkspaceWarningKind.RevisionConflict);
    }

    [Fact]
    public async Task SelectRemoveAndClearAsync_WhenRevisionsMatch_ShouldPersistWorkspaceLifecycle()
    {
        await using var fixture = await WorkspaceFixture.CreateAsync(includeSecondSession: true);
        var ct = TestContext.Current.CancellationToken;
        await fixture.Workspace.InitializeAsync(ct);

        var selected = await fixture.Workspace.SelectSessionAsync("session-2", ct);
        var removed = await fixture.Workspace.RemoveSessionAsync("session-2", ct);
        var cleared = await fixture.Workspace.ClearAsync(ct);
        var catalog = await fixture.Provider.GetCatalogAsync(PARTITION, ct);

        selected.Should().NotBeNull();
        selected!.IsRuntimeActive.Should().BeFalse();
        removed.Should().BeTrue();
        cleared.Should().BeTrue();
        fixture.Workspace.Sessions.Should().BeEmpty();
        fixture.Workspace.CurrentSession.Should().BeNull();
        catalog.Sessions.Should().BeEmpty();
        catalog.CurrentSessionId.Should().BeNull();
    }

    [Fact]
    public async Task AddSessionAsync_WhenDetachedSessionIsValid_ShouldCreateAndSelectDurableConversation()
    {
        await using var fixture = await WorkspaceFixture.CreateAsync();
        var ct = TestContext.Current.CancellationToken;
        var loadResult = await fixture.HistoryFacade.LoadSessionAsync("session-1", ct);
        loadResult.IsFailed(out _, out var detached).Should().BeFalse();
        detached.Should().NotBeNull();
        var catalog = await fixture.Provider.GetCatalogAsync(PARTITION, ct);
        _ = await fixture.Provider.ClearAsync(PARTITION, catalog.Revision, ct);
        await using var workspace = new ChatSessionWorkspace(fixture.HistoryFacade);
        await workspace.InitializeAsync(ct);

        await workspace.AddSessionAsync(detached!, ct);
        var persistedCatalog = await fixture.Provider.GetCatalogAsync(PARTITION, ct);

        workspace.CurrentSessionId.Should().Be("session-1");
        persistedCatalog.CurrentSessionId.Should().Be("session-1");
        persistedCatalog.Sessions.Should().ContainSingle(item => item.SessionId == "session-1");
    }

    private sealed class WorkspaceFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        private WorkspaceFixture(
            ServiceProvider services,
            BrowserChatHistoryProvider provider,
            ChatSessionWorkspace workspace)
        {
            _services = services;
            Provider = provider;
            Workspace = workspace;
            HistoryFacade = services.GetRequiredService<ChatHistoryFacade>();
        }

        internal BrowserChatHistoryProvider Provider { get; }

        internal ChatSessionWorkspace Workspace { get; }

        internal ChatHistoryFacade HistoryFacade { get; }

        internal static async Task<WorkspaceFixture> CreateAsync(bool includeSecondSession = false)
        {
            var storage = new InMemoryBrowserStorage();
            var provider = new BrowserChatHistoryProvider(
                storage,
                Options.Create(new BrowserChatHistoryOptions()),
                storage.HistoryLock);
            var ct = TestContext.Current.CancellationToken;
            var saved = await provider.SaveSessionAsync(PARTITION, CreateSnapshot(), 0, ct);
            if (includeSecondSession)
            {
                saved = await provider.SaveSessionAsync(
                    PARTITION,
                    CreateSnapshot("session-2", "second persisted message"),
                    saved.Revision,
                    ct);
            }

            _ = await provider.SetCurrentSessionAsync(PARTITION, "session-1", saved.Revision, ct);

            var moduleOptions = new ModuleAIOption();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IOptions<ModuleAIOption>>(Options.Create(moduleOptions));
            new ModuleAI(moduleOptions).ConfigureServices(services);
            services.RemoveAll<IChatHistoryProvider>();
            services.RemoveAll<IChatHistoryPartitionResolver>();
            services.AddSingleton<IChatHistoryProvider>(provider);
            services.AddSingleton<IChatHistoryPartitionResolver>(new FixedPartitionResolver());
            var serviceProvider = services.BuildServiceProvider();
            var workspace = new ChatSessionWorkspace(serviceProvider.GetRequiredService<ChatHistoryFacade>());
            return new WorkspaceFixture(serviceProvider, provider, workspace);
        }

        public async ValueTask DisposeAsync()
        {
            await Workspace.DisposeAsync();
            await _services.DisposeAsync();
        }
    }

    private sealed class FixedPartitionResolver : IChatHistoryPartitionResolver
    {
        public ValueTask<ChatHistoryPartition> ResolveAsync(CancellationToken ct = default)
            => ValueTask.FromResult(PARTITION);
    }

    private static ChatSessionSnapshot CreateSnapshot(
        string sessionId = "session-1",
        string message = "persisted message")
    {
        var now = DateTimeOffset.UtcNow;
        return new ChatSessionSnapshot
        {
            SessionId = sessionId,
            Title = $"Persisted {sessionId}",
            CreatedAt = now,
            UpdatedAt = now,
            Settings = new ChatSessionSettings("missing-provider", "missing-model", null, false),
            Turns =
            [
                new ChatTurnSnapshot
                {
                    HistoryCheckpoint = 0,
                    UserMessage = new ChatMessageSnapshot
                    {
                        Id = $"message-{sessionId}",
                        Role = AIChatRole.User,
                        Content = message,
                        CreatedAt = now
                    }
                }
            ]
        };
    }
}
