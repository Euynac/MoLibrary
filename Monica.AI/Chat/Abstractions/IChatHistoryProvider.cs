using Monica.AI.Chat.Models;

namespace Monica.AI.Chat.Abstractions;

/// <summary>
/// Persists complete chat session snapshots inside explicitly resolved partitions.
/// </summary>
/// <remarks>
/// Implementations must apply mutations atomically with the catalog and reject stale expected
/// revision values by returning a conflict result. Every non-success result must expose a
/// provider-neutral <see cref="ChatHistoryWriteFailureReason"/>; warning text is diagnostic only.
/// Implementations own serialized snapshot security, retention, and disposal of external resources.
/// </remarks>
public interface IChatHistoryProvider
{
    /// <summary>Loads the catalog and current selection for a partition.</summary>
    Task<ChatHistoryCatalog> GetCatalogAsync(
        ChatHistoryPartition partition,
        CancellationToken ct = default);

    /// <summary>Loads one complete session snapshot, or <see langword="null"/> when absent.</summary>
    Task<ChatSessionSnapshot?> LoadSessionAsync(
        ChatHistoryPartition partition,
        string sessionId,
        CancellationToken ct = default);

    /// <summary>Saves one complete snapshot when the catalog revision matches.</summary>
    Task<ChatHistoryWriteResult> SaveSessionAsync(
        ChatHistoryPartition partition,
        ChatSessionSnapshot snapshot,
        long expectedRevision,
        CancellationToken ct = default);

    /// <summary>Deletes one session when the catalog revision matches.</summary>
    Task<ChatHistoryWriteResult> DeleteSessionAsync(
        ChatHistoryPartition partition,
        string sessionId,
        long expectedRevision,
        CancellationToken ct = default);

    /// <summary>Clears every persisted session and current selection in a partition.</summary>
    Task<ChatHistoryWriteResult> ClearAsync(
        ChatHistoryPartition partition,
        long expectedRevision,
        CancellationToken ct = default);

    /// <summary>Changes the current-session selection when the catalog revision matches.</summary>
    Task<ChatHistoryWriteResult> SetCurrentSessionAsync(
        ChatHistoryPartition partition,
        string? sessionId,
        long expectedRevision,
        CancellationToken ct = default);
}
