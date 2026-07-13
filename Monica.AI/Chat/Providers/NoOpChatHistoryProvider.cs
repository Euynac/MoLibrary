using Monica.AI.Chat.Abstractions;
using Monica.AI.Chat.Models;

namespace Monica.AI.Chat.Providers;

internal sealed class NoOpChatHistoryProvider : IChatHistoryProvider
{
    public Task<ChatHistoryCatalog> GetCatalogAsync(
        ChatHistoryPartition partition,
        CancellationToken ct = default)
        => Task.FromResult(ChatHistoryCatalog.Empty);

    public Task<ChatSessionSnapshot?> LoadSessionAsync(
        ChatHistoryPartition partition,
        string sessionId,
        CancellationToken ct = default)
        => Task.FromResult<ChatSessionSnapshot?>(null);

    public Task<ChatHistoryWriteResult> SaveSessionAsync(
        ChatHistoryPartition partition,
        ChatSessionSnapshot snapshot,
        long expectedRevision,
        CancellationToken ct = default)
        => Task.FromResult(ChatHistoryWriteResult.PersistenceDisabled);

    public Task<ChatHistoryWriteResult> DeleteSessionAsync(
        ChatHistoryPartition partition,
        string sessionId,
        long expectedRevision,
        CancellationToken ct = default)
        => Task.FromResult(ChatHistoryWriteResult.PersistenceDisabled);

    public Task<ChatHistoryWriteResult> ClearAsync(
        ChatHistoryPartition partition,
        long expectedRevision,
        CancellationToken ct = default)
        => Task.FromResult(ChatHistoryWriteResult.PersistenceDisabled);

    public Task<ChatHistoryWriteResult> SetCurrentSessionAsync(
        ChatHistoryPartition partition,
        string? sessionId,
        long expectedRevision,
        CancellationToken ct = default)
        => Task.FromResult(ChatHistoryWriteResult.PersistenceDisabled);
}

internal sealed class NoOpChatHistoryPartitionResolver : IChatHistoryPartitionResolver
{
    private static readonly ChatHistoryPartition DISABLED_PARTITION = new("disabled");

    public ValueTask<ChatHistoryPartition> ResolveAsync(CancellationToken ct = default)
        => ValueTask.FromResult(DISABLED_PARTITION);
}
