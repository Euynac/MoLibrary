using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Monica.AI.Chat.Models;
using Monica.AI.Models;
using Monica.AI.UI.UIChat.Models;
using Monica.AI.UI.UIChat.Providers.Browser;
using Monica.UI.Shell.Support;
using Test.Monica.AI.UI.Support;

namespace Test.Monica.AI.UI.UIChat.Providers.Browser;

public sealed class BrowserChatHistoryProviderTests
{
    private static readonly ChatHistoryPartition PARTITION = new("browser-test");

    [Fact]
    public async Task SaveSessionAsync_WhenRevisionMatches_ShouldReconstructSnapshotAndCatalog()
    {
        var storage = new InMemoryBrowserStorage();
        var provider = CreateProvider(storage);
        var snapshot = CreateSnapshot("session-1", CreateIncompressibleContent());
        var ct = TestContext.Current.CancellationToken;

        var result = await provider.SaveSessionAsync(PARTITION, snapshot, expectedRevision: 0, ct: ct);
        var catalog = await provider.GetCatalogAsync(PARTITION, ct);
        var restored = await provider.LoadSessionAsync(PARTITION, snapshot.SessionId, ct);

        result.IsPersisted.Should().BeTrue();
        result.Revision.Should().Be(1);
        catalog.Revision.Should().Be(1);
        catalog.Sessions.Should().ContainSingle(item => item.SessionId == snapshot.SessionId);
        restored.Should().NotBeNull();
        restored!.Turns[0].UserMessage.Content.Should().Be(snapshot.Turns[0].UserMessage.Content);
        storage.Values.Keys.Count(key => key.Contains(":chunk:", StringComparison.Ordinal)).Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task SaveSessionAsync_WhenExpectedRevisionIsStale_ShouldRejectWrite()
    {
        var storage = new InMemoryBrowserStorage();
        var provider = CreateProvider(storage);
        var ct = TestContext.Current.CancellationToken;
        _ = await provider.SaveSessionAsync(PARTITION, CreateSnapshot("session-1"), 0, ct);

        var conflict = await provider.SaveSessionAsync(PARTITION, CreateSnapshot("session-2"), 0, ct);

        conflict.IsConflict.Should().BeTrue();
        conflict.Revision.Should().Be(1);
        (await provider.GetCatalogAsync(PARTITION, ct)).Sessions.Should().ContainSingle();
    }

    [Fact]
    public async Task SaveSessionAsync_WhenRetentionIsExceeded_ShouldPreserveCurrentSession()
    {
        var storage = new InMemoryBrowserStorage();
        var provider = CreateProvider(storage, maxSessions: 2);
        var ct = TestContext.Current.CancellationToken;
        var first = await provider.SaveSessionAsync(PARTITION, CreateSnapshot("current", updatedOffset: -3), 0, ct);
        var selected = await provider.SetCurrentSessionAsync(PARTITION, "current", first.Revision, ct);
        var second = await provider.SaveSessionAsync(
            PARTITION,
            CreateSnapshot("old-inactive", updatedOffset: -2),
            selected.Revision,
            ct);

        var third = await provider.SaveSessionAsync(
            PARTITION,
            CreateSnapshot("newest", updatedOffset: -1),
            second.Revision,
            ct);
        var catalog = await provider.GetCatalogAsync(PARTITION, ct);

        third.PrunedSessionIds.Should().ContainSingle().Which.Should().Be("old-inactive");
        catalog.Sessions.Select(item => item.SessionId).Should().BeEquivalentTo("current", "newest");
        catalog.CurrentSessionId.Should().Be("current");
    }

    [Fact]
    public async Task SaveSessionAsync_WhenQuotaFails_ShouldPruneOldestInactiveAndRetry()
    {
        var storage = new InMemoryBrowserStorage();
        var provider = CreateProvider(storage, maxSessions: 3);
        var ct = TestContext.Current.CancellationToken;
        var first = await provider.SaveSessionAsync(PARTITION, CreateSnapshot("current", updatedOffset: -3), 0, ct);
        var selected = await provider.SetCurrentSessionAsync(PARTITION, "current", first.Revision, ct);
        var second = await provider.SaveSessionAsync(
            PARTITION,
            CreateSnapshot("old-inactive", updatedOffset: -2),
            selected.Revision,
            ct);
        storage.Operations.Clear();
        storage.WriteFailure = key =>
            key.Contains(":session:new:", StringComparison.Ordinal)
            && storage.RemovedKeys.All(removed => !removed.Contains(":session:old-inactive:", StringComparison.Ordinal))
                ? BrowserStorageWriteFailureKind.QuotaExceeded
                : BrowserStorageWriteFailureKind.None;

        var result = await provider.SaveSessionAsync(
            PARTITION,
            CreateSnapshot("new", updatedOffset: -1),
            second.Revision,
            ct);
        var catalog = await provider.GetCatalogAsync(PARTITION, ct);

        result.IsPersisted.Should().BeTrue();
        result.PrunedSessionIds.Should().Contain("old-inactive");
        catalog.Sessions.Select(item => item.SessionId).Should().BeEquivalentTo("current", "new");
        var removedSnapshotIndex = storage.Operations.FindIndex(operation =>
            operation.StartsWith("remove:", StringComparison.Ordinal)
            && operation.Contains(":session:old-inactive:", StringComparison.Ordinal));
        var committedPruneIndex = storage.Operations.FindIndex(operation =>
            operation.StartsWith("write:", StringComparison.Ordinal)
            && operation.EndsWith(":head", StringComparison.Ordinal));
        committedPruneIndex.Should().BeGreaterThanOrEqualTo(0);
        removedSnapshotIndex.Should().BeGreaterThan(committedPruneIndex);
    }

    [Fact]
    public async Task SetCurrentSessionAsync_WhenNewSessionExceedsRetention_ShouldKeepNewCurrentOnly()
    {
        var storage = new InMemoryBrowserStorage();
        var provider = CreateProvider(storage, maxSessions: 1);
        var ct = TestContext.Current.CancellationToken;
        var first = await provider.SaveSessionAsync(PARTITION, CreateSnapshot("old-current", updatedOffset: -2), 0, ct);
        var selected = await provider.SetCurrentSessionAsync(PARTITION, "old-current", first.Revision, ct);
        var saved = await provider.SaveSessionAsync(
            PARTITION,
            CreateSnapshot("new-current", updatedOffset: -1),
            selected.Revision,
            ct);

        var changed = await provider.SetCurrentSessionAsync(PARTITION, "new-current", saved.Revision, ct);
        var catalog = await provider.GetCatalogAsync(PARTITION, ct);

        changed.PrunedSessionIds.Should().ContainSingle().Which.Should().Be("old-current");
        catalog.CurrentSessionId.Should().Be("new-current");
        catalog.Sessions.Should().ContainSingle(item => item.SessionId == "new-current");
    }

    [Fact]
    public async Task SaveSessionAsync_WhenChunkWriteFails_ShouldRemoveEveryPartialChunk()
    {
        var storage = new InMemoryBrowserStorage();
        var provider = CreateProvider(storage);
        storage.WriteFailure = key => key.Contains(":session:partial:1:chunk:1", StringComparison.Ordinal)
            ? BrowserStorageWriteFailureKind.StorageUnavailable
            : BrowserStorageWriteFailureKind.None;

        var result = await provider.SaveSessionAsync(
            PARTITION,
            CreateSnapshot("partial", CreateIncompressibleContent()),
            0,
            TestContext.Current.CancellationToken);

        result.IsPersisted.Should().BeFalse();
        storage.Values.Keys.Should().NotContain(key =>
            key.Contains(":session:partial:1:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveSessionAsync_WhenTwoScopesUseSameRevision_ShouldAllowOneAtomicWinner()
    {
        var storage = new InMemoryBrowserStorage();
        var firstProvider = CreateProvider(storage);
        var secondProvider = CreateProvider(storage);
        var ct = TestContext.Current.CancellationToken;

        var results = await Task.WhenAll(
            firstProvider.SaveSessionAsync(PARTITION, CreateSnapshot("first"), 0, ct),
            secondProvider.SaveSessionAsync(PARTITION, CreateSnapshot("second"), 0, ct));

        results.Count(result => result.IsPersisted).Should().Be(1);
        results.Count(result => result.IsConflict).Should().Be(1);
    }

    [Fact]
    public async Task SaveSessionAsync_WhenSnapshotRoleIsInvalid_ShouldRejectUntrustedData()
    {
        var storage = new InMemoryBrowserStorage();
        var provider = CreateProvider(storage);
        var snapshot = CreateSnapshot("invalid") with
        {
            Turns =
            [
                new ChatTurnSnapshot
                {
                    HistoryCheckpoint = 0,
                    UserMessage = new ChatMessageSnapshot
                    {
                        Id = "invalid-role",
                        Role = AIChatRole.Assistant,
                        Content = "not a user",
                        CreatedAt = DateTimeOffset.UtcNow
                    }
                }
            ]
        };

        var save = async () => await provider.SaveSessionAsync(
            PARTITION,
            snapshot,
            0,
            TestContext.Current.CancellationToken);

        await save.Should().ThrowAsync<InvalidDataException>();
    }

    private static BrowserChatHistoryProvider CreateProvider(
        InMemoryBrowserStorage storage,
        int maxSessions = 30)
        => new(
            storage,
            Options.Create(new BrowserChatHistoryOptions { MaxSessions = maxSessions }),
            storage.HistoryLock);

    private static ChatSessionSnapshot CreateSnapshot(
        string sessionId,
        string content = "hello",
        int updatedOffset = 0)
    {
        var updatedAt = DateTimeOffset.UtcNow.AddMinutes(updatedOffset);
        return new ChatSessionSnapshot
        {
            SessionId = sessionId,
            Title = sessionId,
            CreatedAt = updatedAt.AddMinutes(-1),
            UpdatedAt = updatedAt,
            Settings = new ChatSessionSettings("provider", "model", null, false),
            Turns =
            [
                new ChatTurnSnapshot
                {
                    HistoryCheckpoint = 0,
                    UserMessage = new ChatMessageSnapshot
                    {
                        Id = $"{sessionId}-message",
                        Role = AIChatRole.User,
                        Content = content,
                        CreatedAt = updatedAt
                    }
                }
            ]
        };
    }

    private static string CreateIncompressibleContent()
    {
        var bytes = new byte[100_000];
        new Random(42).NextBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
