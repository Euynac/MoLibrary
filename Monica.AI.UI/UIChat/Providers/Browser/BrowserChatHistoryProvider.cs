using Microsoft.Extensions.Options;
using Monica.AI.Chat.Abstractions;
using Monica.AI.Chat.Models;
using Monica.AI.Models;
using Monica.AI.UI.UIChat.Models;
using Monica.UI.Shell.Support;

namespace Monica.AI.UI.UIChat.Providers.Browser;

internal sealed class BrowserChatHistoryProvider(
    IBrowserStorage browserStorage,
    IOptions<BrowserChatHistoryOptions> options,
    IBrowserChatHistoryLock browserLock) : IChatHistoryProvider
{
    private const string CATEGORY = "ai-chat-history";
    private const int MAX_CHUNK_COUNT = 4096;

    private readonly int _maxSessions = ValidateMaxSessions(options.Value.MaxSessions);

    public async Task<ChatHistoryCatalog> GetCatalogAsync(
        ChatHistoryPartition partition,
        CancellationToken ct = default)
    {
        await using var lease = await browserLock.AcquireAsync(partition.Key, ct);
        return await LoadCatalogAsync(partition);
    }

    public async Task<ChatSessionSnapshot?> LoadSessionAsync(
        ChatHistoryPartition partition,
        string sessionId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        await using var lease = await browserLock.AcquireAsync(partition.Key, ct);
        var catalog = await LoadCatalogAsync(partition);
        var summary = catalog.Sessions.FirstOrDefault(candidate => candidate.SessionId == sessionId);
        return summary is null
            ? null
            : await ReadSnapshotAsync(partition, summary.SessionId, summary.Revision);
    }

    public async Task<ChatHistoryWriteResult> SaveSessionAsync(
        ChatHistoryPartition partition,
        ChatSessionSnapshot snapshot,
        long expectedRevision,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSnapshot(snapshot);
        await using var lease = await browserLock.AcquireAsync(partition.Key, ct);
        var catalog = await LoadCatalogAsync(partition);
        if (catalog.Revision != expectedRevision)
        {
            return Conflict(catalog.Revision);
        }

        var pruned = new List<ChatSessionSummary>();
        ChatSessionSnapshot persistedSnapshot;
        BrowserStorageWriteResult writeResult;
        while (true)
        {
            persistedSnapshot = snapshot with { Revision = checked(catalog.Revision + 1) };
            writeResult = await WriteSnapshotAsync(partition, persistedSnapshot);
            if (writeResult.Succeeded)
            {
                break;
            }

            if (writeResult.FailureKind != BrowserStorageWriteFailureKind.QuotaExceeded)
            {
                return NotPersisted(writeResult.FailureKind, catalog.Revision, pruned);
            }

            var candidate = FindPruneCandidate(catalog, persistedSnapshot.SessionId, pruned);
            if (candidate is null)
            {
                return NotPersisted(writeResult.FailureKind, catalog.Revision, pruned);
            }

            // Commit the catalog pointer before deleting the candidate snapshot, preserving the
            // invariant that every catalog-visible summary has readable data.
            var prunedCatalog = new ChatHistoryCatalog
            {
                Revision = checked(catalog.Revision + 1),
                CurrentSessionId = catalog.CurrentSessionId,
                Sessions = catalog.Sessions
                    .Where(item => item.SessionId != candidate.SessionId)
                    .ToArray()
            };
            var pruneCommit = await CommitCatalogAsync(partition, catalog, prunedCatalog);
            if (!pruneCommit.Succeeded)
            {
                return NotPersisted(pruneCommit.FailureKind, catalog.Revision, pruned);
            }

            await RemoveSnapshotAsync(partition, candidate.SessionId, candidate.Revision);
            pruned.Add(candidate);
            catalog = prunedCatalog;
        }

        var sessions = catalog.Sessions
            .Where(summary => summary.SessionId != persistedSnapshot.SessionId)
            .Where(summary => pruned.All(item => item.SessionId != summary.SessionId))
            .Append(persistedSnapshot.ToSummary())
            .OrderByDescending(summary => summary.UpdatedAt)
            .ToList();

        AddRetentionCandidates(catalog, persistedSnapshot.SessionId, sessions, pruned);
        sessions.RemoveAll(summary => pruned.Any(item => item.SessionId == summary.SessionId));

        var nextCatalog = new ChatHistoryCatalog
        {
            Revision = persistedSnapshot.Revision,
            CurrentSessionId = catalog.CurrentSessionId,
            Sessions = sessions
        };

        var commitResult = await CommitCatalogAsync(partition, catalog, nextCatalog);
        if (!commitResult.Succeeded)
        {
            await RemoveSnapshotAsync(partition, persistedSnapshot.SessionId, persistedSnapshot.Revision);
            return NotPersisted(commitResult.FailureKind, catalog.Revision, pruned);
        }

        var previous = catalog.Sessions.FirstOrDefault(item => item.SessionId == persistedSnapshot.SessionId);
        if (previous is not null)
        {
            await RemoveSnapshotAsync(partition, previous.SessionId, previous.Revision);
        }

        foreach (var removed in pruned)
        {
            await RemoveSnapshotAsync(partition, removed.SessionId, removed.Revision);
        }

        return Succeeded(persistedSnapshot.Revision, pruned.Select(item => item.SessionId));
    }

    public async Task<ChatHistoryWriteResult> DeleteSessionAsync(
        ChatHistoryPartition partition,
        string sessionId,
        long expectedRevision,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return await MutateCatalogAsync(
            partition,
            expectedRevision,
            catalog =>
            {
                var sessions = catalog.Sessions.Where(item => item.SessionId != sessionId).ToArray();
                var currentSessionId = string.Equals(catalog.CurrentSessionId, sessionId, StringComparison.Ordinal)
                    ? sessions.FirstOrDefault()?.SessionId
                    : catalog.CurrentSessionId;
                return (sessions, currentSessionId);
            },
            async catalog =>
            {
                var removed = catalog.Sessions.FirstOrDefault(item => item.SessionId == sessionId);
                if (removed is not null)
                {
                    await RemoveSnapshotAsync(partition, removed.SessionId, removed.Revision);
                }
            },
            ct);
    }

    public async Task<ChatHistoryWriteResult> ClearAsync(
        ChatHistoryPartition partition,
        long expectedRevision,
        CancellationToken ct = default)
    {
        return await MutateCatalogAsync(
            partition,
            expectedRevision,
            _ => (Array.Empty<ChatSessionSummary>(), null),
            async catalog =>
            {
                foreach (var summary in catalog.Sessions)
                {
                    await RemoveSnapshotAsync(partition, summary.SessionId, summary.Revision);
                }
            },
            ct);
    }

    public async Task<ChatHistoryWriteResult> SetCurrentSessionAsync(
        ChatHistoryPartition partition,
        string? sessionId,
        long expectedRevision,
        CancellationToken ct = default)
    {
        await using var lease = await browserLock.AcquireAsync(partition.Key, ct);
        var catalog = await LoadCatalogAsync(partition);
        if (catalog.Revision != expectedRevision)
        {
            return Conflict(catalog.Revision);
        }

        if (sessionId is not null && catalog.Sessions.All(item => item.SessionId != sessionId))
        {
            throw new KeyNotFoundException($"Chat session '{sessionId}' was not found.");
        }

        var sessions = catalog.Sessions.ToList();
        var pruned = sessions
            .Where(item => item.SessionId != sessionId)
            .OrderBy(item => item.UpdatedAt)
            .Take(Math.Max(0, sessions.Count - _maxSessions))
            .ToArray();
        sessions.RemoveAll(item => pruned.Any(candidate => candidate.SessionId == item.SessionId));
        var revision = checked(catalog.Revision + 1);
        var nextCatalog = new ChatHistoryCatalog
        {
            Revision = revision,
            CurrentSessionId = sessionId,
            Sessions = sessions
        };
        var commitResult = await CommitCatalogAsync(partition, catalog, nextCatalog);
        if (!commitResult.Succeeded)
        {
            return NotPersisted(commitResult.FailureKind, catalog.Revision);
        }

        foreach (var removed in pruned)
        {
            await RemoveSnapshotAsync(partition, removed.SessionId, removed.Revision);
        }

        return Succeeded(revision, pruned.Select(item => item.SessionId));
    }

    private async Task<ChatHistoryWriteResult> MutateCatalogAsync(
        ChatHistoryPartition partition,
        long expectedRevision,
        Func<ChatHistoryCatalog, (IReadOnlyList<ChatSessionSummary> Sessions, string? CurrentSessionId)> mutate,
        Func<ChatHistoryCatalog, Task> afterCommit,
        CancellationToken ct)
    {
        await using var lease = await browserLock.AcquireAsync(partition.Key, ct);
        var catalog = await LoadCatalogAsync(partition);
        if (catalog.Revision != expectedRevision)
        {
            return Conflict(catalog.Revision);
        }

        var mutation = mutate(catalog);
        var revision = checked(catalog.Revision + 1);
        var nextCatalog = new ChatHistoryCatalog
        {
            Revision = revision,
            CurrentSessionId = mutation.CurrentSessionId,
            Sessions = mutation.Sessions
        };
        var commitResult = await CommitCatalogAsync(partition, catalog, nextCatalog);
        if (!commitResult.Succeeded)
        {
            return NotPersisted(commitResult.FailureKind, catalog.Revision);
        }

        await afterCommit(catalog);
        return Succeeded(revision, []);
    }

    private async Task<ChatHistoryCatalog> LoadCatalogAsync(ChatHistoryPartition partition)
    {
        var head = await browserStorage.GetAsync<BrowserChatHistoryCatalogHead?>(HeadKey(partition), null);
        if (head is null)
        {
            return ChatHistoryCatalog.Empty;
        }

        var current = await TryReadCatalogAsync(partition, head.CurrentRevision);
        if (current is not null)
        {
            return current;
        }

        if (head.PreviousRevision is { } previousRevision)
        {
            var previous = await TryReadCatalogAsync(partition, previousRevision);
            if (previous is not null)
            {
                return previous;
            }
        }

        throw new InvalidDataException("The browser chat history catalog is corrupt.");
    }

    private async Task<ChatHistoryCatalog?> TryReadCatalogAsync(
        ChatHistoryPartition partition,
        long revision)
    {
        var document = await browserStorage.GetAsync<BrowserChatHistoryInlineDocument?>(
            CatalogKey(partition, revision),
            null);
        if (document is null)
        {
            return null;
        }

        try
        {
            var catalog = BrowserChatHistoryCodec.Decode<ChatHistoryCatalog>([document.Payload], document.Sha256);
            ValidateCatalog(catalog);
            return catalog;
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException)
        {
            return null;
        }
    }

    private async Task<ChatSessionSnapshot> ReadSnapshotAsync(
        ChatHistoryPartition partition,
        string sessionId,
        long revision)
    {
        var manifest = await browserStorage.GetAsync<BrowserChatHistorySnapshotManifest?>(
            SnapshotManifestKey(partition, sessionId, revision),
            null);
        if (manifest is null
            || manifest.ChunkCount is <= 0 or > MAX_CHUNK_COUNT
            || string.IsNullOrWhiteSpace(manifest.Sha256)
            || manifest.Sha256.Length != 64)
        {
            throw new InvalidDataException($"Chat session '{sessionId}' is missing its browser manifest.");
        }

        var chunks = new string[manifest.ChunkCount];
        for (var index = 0; index < chunks.Length; index++)
        {
            chunks[index] = await browserStorage.GetAsync<string?>(
                                SnapshotChunkKey(partition, sessionId, revision, index),
                                null)
                            ?? throw new InvalidDataException($"Chat session '{sessionId}' is missing chunk {index}.");
        }

        var snapshot = BrowserChatHistoryCodec.Decode<ChatSessionSnapshot>(chunks, manifest.Sha256);
        ValidateSnapshot(snapshot, sessionId, revision);
        return snapshot;
    }

    private async Task<BrowserStorageWriteResult> WriteSnapshotAsync(
        ChatHistoryPartition partition,
        ChatSessionSnapshot snapshot)
    {
        var encoded = BrowserChatHistoryCodec.Encode(snapshot);
        for (var index = 0; index < encoded.Chunks.Count; index++)
        {
            var result = await browserStorage.TrySetAsync(
                SnapshotChunkKey(partition, snapshot.SessionId, snapshot.Revision, index),
                encoded.Chunks[index]);
            if (!result.Succeeded)
            {
                await RemoveSnapshotVersionAsync(
                    partition,
                    snapshot.SessionId,
                    snapshot.Revision,
                    encoded.Chunks.Count);
                return result;
            }
        }

        var manifestResult = await browserStorage.TrySetAsync(
            SnapshotManifestKey(partition, snapshot.SessionId, snapshot.Revision),
            new BrowserChatHistorySnapshotManifest(encoded.Chunks.Count, encoded.Sha256));
        if (!manifestResult.Succeeded)
        {
            await RemoveSnapshotVersionAsync(
                partition,
                snapshot.SessionId,
                snapshot.Revision,
                encoded.Chunks.Count);
        }

        return manifestResult;
    }

    private async Task<BrowserStorageWriteResult> CommitCatalogAsync(
        ChatHistoryPartition partition,
        ChatHistoryCatalog previousCatalog,
        ChatHistoryCatalog nextCatalog)
    {
        var previousHead = await browserStorage.GetAsync<BrowserChatHistoryCatalogHead?>(
            HeadKey(partition),
            null);
        var encoded = BrowserChatHistoryCodec.Encode(nextCatalog);
        var document = new BrowserChatHistoryInlineDocument(string.Concat(encoded.Chunks), encoded.Sha256);
        var catalogWrite = await browserStorage.TrySetAsync(
            CatalogKey(partition, nextCatalog.Revision),
            document);
        if (!catalogWrite.Succeeded)
        {
            return catalogWrite;
        }

        var headWrite = await browserStorage.TrySetAsync(
            HeadKey(partition),
            new BrowserChatHistoryCatalogHead(
                nextCatalog.Revision,
                previousCatalog.Revision == nextCatalog.Revision ? null : previousCatalog.Revision));
        if (!headWrite.Succeeded)
        {
            await RemoveCatalogAsync(partition, nextCatalog.Revision);
        }
        else if (previousHead?.PreviousRevision is { } obsoleteRevision
                 && obsoleteRevision != previousCatalog.Revision
                 && obsoleteRevision != nextCatalog.Revision)
        {
            await RemoveCatalogAsync(partition, obsoleteRevision);
        }

        return headWrite;
    }

    private static ChatSessionSummary? FindPruneCandidate(
        ChatHistoryCatalog catalog,
        string savedSessionId,
        IReadOnlyCollection<ChatSessionSummary> alreadyPruned)
        => catalog.Sessions
            .Where(item => item.SessionId != savedSessionId)
            .Where(item => item.SessionId != catalog.CurrentSessionId)
            .Where(item => alreadyPruned.All(pruned => pruned.SessionId != item.SessionId))
            .OrderBy(item => item.UpdatedAt)
            .FirstOrDefault();

    private void AddRetentionCandidates(
        ChatHistoryCatalog catalog,
        string savedSessionId,
        IReadOnlyList<ChatSessionSummary> sessions,
        ICollection<ChatSessionSummary> pruned)
    {
        var excess = sessions.Count - _maxSessions;
        if (excess <= 0)
        {
            return;
        }

        foreach (var candidate in sessions
                     .Where(item => item.SessionId != savedSessionId)
                     .Where(item => item.SessionId != catalog.CurrentSessionId)
                     .Where(item => pruned.All(prunedItem => prunedItem.SessionId != item.SessionId))
                     .OrderBy(item => item.UpdatedAt)
                     .Take(excess))
        {
            pruned.Add(candidate);
        }
    }

    private async Task RemoveSnapshotAsync(
        ChatHistoryPartition partition,
        string sessionId,
        long revision)
    {
        var manifest = await browserStorage.GetAsync<BrowserChatHistorySnapshotManifest?>(
            SnapshotManifestKey(partition, sessionId, revision),
            null);
        if (manifest is not null)
        {
            for (var index = 0; index < manifest.ChunkCount; index++)
            {
                await browserStorage.RemoveAsync(SnapshotChunkKey(partition, sessionId, revision, index));
            }
        }

        await browserStorage.RemoveAsync(SnapshotManifestKey(partition, sessionId, revision));
    }

    private async Task RemoveSnapshotVersionAsync(
        ChatHistoryPartition partition,
        string sessionId,
        long revision,
        int chunkCount)
    {
        for (var index = 0; index < chunkCount; index++)
        {
            await browserStorage.RemoveAsync(SnapshotChunkKey(partition, sessionId, revision, index));
        }

        await browserStorage.RemoveAsync(SnapshotManifestKey(partition, sessionId, revision));
    }

    private Task RemoveCatalogAsync(ChatHistoryPartition partition, long revision)
        => browserStorage.RemoveAsync(CatalogKey(partition, revision));

    private static ChatHistoryWriteResult Conflict(long revision) => new()
    {
        Status = ChatHistoryWriteStatus.Conflict,
        Revision = revision,
        FailureReason = ChatHistoryWriteFailureReason.Conflict
    };

    private static ChatHistoryWriteResult Succeeded(long revision, IEnumerable<string> prunedSessionIds)
    {
        var pruned = prunedSessionIds.ToArray();
        return new ChatHistoryWriteResult
        {
            Status = ChatHistoryWriteStatus.Succeeded,
            Revision = revision,
            PrunedSessionIds = pruned
        };
    }

    private static ChatHistoryWriteResult NotPersisted(
        BrowserStorageWriteFailureKind failureKind,
        long revision,
        IEnumerable<ChatSessionSummary>? prunedSessions = null) => new()
    {
        Status = ChatHistoryWriteStatus.NotPersisted,
        Revision = revision,
        PrunedSessionIds = prunedSessions?.Select(item => item.SessionId).ToArray() ?? [],
        FailureReason = failureKind switch
        {
            BrowserStorageWriteFailureKind.QuotaExceeded => ChatHistoryWriteFailureReason.QuotaExceeded,
            BrowserStorageWriteFailureKind.SerializationFailed => ChatHistoryWriteFailureReason.SerializationFailed,
            BrowserStorageWriteFailureKind.StorageUnavailable => ChatHistoryWriteFailureReason.StorageUnavailable,
            _ => ChatHistoryWriteFailureReason.Unknown
        }
    };

    private static int ValidateMaxSessions(int maxSessions)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSessions);
        return maxSessions;
    }

    private static void ValidateCatalog(ChatHistoryCatalog catalog)
    {
        if (catalog.Version != ChatHistoryCatalog.CurrentVersion || catalog.Revision < 0)
        {
            throw new InvalidDataException("The browser chat history catalog version or revision is invalid.");
        }

        if (catalog.Sessions is null
            || catalog.Sessions.Any(summary =>
                summary is null
                ||
                summary.Version != ChatSessionSummary.CurrentVersion
                || string.IsNullOrWhiteSpace(summary.SessionId)
                || string.IsNullOrWhiteSpace(summary.Title)
                || summary.Revision <= 0)
            || catalog.Sessions.Select(summary => summary.SessionId).Distinct(StringComparer.Ordinal).Count()
            != catalog.Sessions.Count
            || catalog.CurrentSessionId is not null
            && catalog.Sessions.All(summary => summary.SessionId != catalog.CurrentSessionId))
        {
            throw new InvalidDataException("The browser chat history catalog contains invalid session metadata.");
        }
    }

    private static void ValidateSnapshot(
        ChatSessionSnapshot snapshot,
        string? expectedSessionId = null,
        long? expectedRevision = null)
    {
        if (snapshot.Version != ChatSessionSnapshot.CurrentVersion
            || string.IsNullOrWhiteSpace(snapshot.SessionId)
            || string.IsNullOrWhiteSpace(snapshot.Title)
            || snapshot.Settings is null
            || string.IsNullOrWhiteSpace(snapshot.Settings.ProviderId)
            || snapshot.Turns is null
            || snapshot.AgentHistoryMessageCount < 0
            || expectedSessionId is not null
            && !string.Equals(snapshot.SessionId, expectedSessionId, StringComparison.Ordinal)
            || expectedRevision is not null && snapshot.Revision != expectedRevision)
        {
            throw new InvalidDataException("The browser chat history snapshot metadata is invalid.");
        }

        var messageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var turn in snapshot.Turns)
        {
            if (turn is null
                || turn.UserMessage is null
                || turn.ErrorMessages is null
                || turn.HistoryCheckpoint < 0
                || turn.HistoryCheckpoint > snapshot.AgentHistoryMessageCount
                || turn.UserMessage.Role != AIChatRole.User
                || string.IsNullOrWhiteSpace(turn.UserMessage.Content))
            {
                throw new InvalidDataException("The browser chat history contains an invalid user turn.");
            }

            ValidateMessage(turn.UserMessage, messageIds);
            if (turn.AssistantMessage is not null)
            {
                if (turn.AssistantMessage.Role != AIChatRole.Assistant
                    || turn.AssistantMessage.Kind != AIChatMessageKind.Message)
                {
                    throw new InvalidDataException("The browser chat history contains an invalid assistant message.");
                }

                ValidateMessage(turn.AssistantMessage, messageIds);
            }

            foreach (var error in turn.ErrorMessages)
            {
                if (error.Role != AIChatRole.Assistant
                    || error.Kind != AIChatMessageKind.Error
                    || string.IsNullOrWhiteSpace(error.Content))
                {
                    throw new InvalidDataException("The browser chat history contains an invalid error entry.");
                }

                ValidateMessage(error, messageIds);
            }
        }
    }

    private static void ValidateMessage(
        ChatMessageSnapshot message,
        ISet<string> messageIds)
    {
        if (string.IsNullOrWhiteSpace(message.Id) || !messageIds.Add(message.Id))
        {
            throw new InvalidDataException("The browser chat history contains a missing or duplicate message identifier.");
        }
    }

    private static string Root(ChatHistoryPartition partition) => $"{CATEGORY}:{partition.Key}";

    private static string HeadKey(ChatHistoryPartition partition) => $"{Root(partition)}:head";

    private static string CatalogKey(ChatHistoryPartition partition, long revision)
        => $"{Root(partition)}:catalog:{revision}";

    private static string SnapshotManifestKey(
        ChatHistoryPartition partition,
        string sessionId,
        long revision) => $"{Root(partition)}:session:{sessionId}:{revision}:manifest";

    private static string SnapshotChunkKey(
        ChatHistoryPartition partition,
        string sessionId,
        long revision,
        int index) => $"{Root(partition)}:session:{sessionId}:{revision}:chunk:{index}";

}
