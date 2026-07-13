using AwesomeAssertions;
using Monica.AI.UI.UIChat.Providers.Browser;
using Monica.UI.Shell.Support;
using Test.Monica.AI.UI.Support;

namespace Test.Monica.AI.UI.UIChat.Providers.Browser;

public sealed class BrowserChatHistoryPartitionResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenFirstCallsRace_ShouldPersistAndReturnOneInstallationId()
    {
        var storage = new InMemoryBrowserStorage();
        using var resolver = new BrowserChatHistoryPartitionResolver(storage, storage.HistoryLock);
        var ct = TestContext.Current.CancellationToken;

        var partitions = await Task.WhenAll(
            resolver.ResolveAsync(ct).AsTask(),
            resolver.ResolveAsync(ct).AsTask());

        partitions[0].Should().Be(partitions[1]);
        storage.Operations.Count(operation =>
                operation == "write:ai-chat-history:installation-id")
            .Should().Be(1);
    }

    [Fact]
    public async Task ResolveAsync_WhenInstallationIdWriteFails_ShouldNotCacheGeneratedId()
    {
        var storage = new InMemoryBrowserStorage
        {
            WriteFailure = key => key == "ai-chat-history:installation-id"
                ? BrowserStorageWriteFailureKind.StorageUnavailable
                : BrowserStorageWriteFailureKind.None
        };
        using var resolver = new BrowserChatHistoryPartitionResolver(storage, storage.HistoryLock);

        var firstResolve = async () => await resolver.ResolveAsync(TestContext.Current.CancellationToken);
        await firstResolve.Should().ThrowAsync<InvalidOperationException>();
        storage.WriteFailure = null;

        var partition = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        partition.Key.Should().NotBeNullOrWhiteSpace();
        storage.Values.Should().ContainKey("ai-chat-history:installation-id");
    }
}
