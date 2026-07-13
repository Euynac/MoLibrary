using Monica.AI.Chat.Abstractions;
using Monica.AI.Chat.Models;
using Monica.AI.Models;
using Monica.AI.Services;
using Monica.Core.Extensions;
using Monica.Core.Results;

namespace Monica.AI.Chat.Facades;

/// <summary>Host-facing entry point for durable chat history operations.</summary>
public sealed class ChatHistoryFacade
{
    private readonly IChatHistoryProvider _historyProvider;
    private readonly IChatHistoryPartitionResolver _partitionResolver;
    private readonly AIChatService _chatService;

    internal ChatHistoryFacade(
        IChatHistoryProvider historyProvider,
        IChatHistoryPartitionResolver partitionResolver,
        AIChatService chatService)
    {
        _historyProvider = historyProvider;
        _partitionResolver = partitionResolver;
        _chatService = chatService;
    }

    /// <summary>Loads the persisted catalog and current-session selection.</summary>
    public async Task<Res<ChatHistoryCatalog>> GetCatalogAsync(CancellationToken ct = default)
    {
        try
        {
            var partition = await _partitionResolver.ResolveAsync(ct);
            return Res.Ok(await _historyProvider.GetCatalogAsync(partition, ct));
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    /// <summary>
    /// Loads a session's transcript as a runtime-lazy <see cref="ChatSession"/>.
    /// </summary>
    public async Task<Res<ChatSession?>> LoadSessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            var partition = await _partitionResolver.ResolveAsync(ct);
            var snapshot = await _historyProvider.LoadSessionAsync(partition, sessionId, ct);
            return Res.Ok<ChatSession?>(snapshot is null
                ? null
                : _chatService.RestoreSession(snapshot, expectedSessionId: sessionId));
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    /// <summary>Saves a complete snapshot when <paramref name="expectedRevision"/> is current.</summary>
    public async Task<Res<ChatHistoryWriteResult>> SaveSessionAsync(
        ChatSession session,
        long expectedRevision,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
            var partition = await _partitionResolver.ResolveAsync(ct);
            var snapshot = await _chatService.CreateSnapshotAsync(session, ct);
            var result = await _historyProvider.SaveSessionAsync(
                partition,
                snapshot,
                expectedRevision,
                ct);
            if (result.IsPersisted)
            {
                session.MarkPersisted(result.Revision);
            }

            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    /// <summary>Deletes one persisted session when the expected catalog revision is current.</summary>
    public async Task<Res<ChatHistoryWriteResult>> DeleteSessionAsync(
        string sessionId,
        long expectedRevision,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
            var partition = await _partitionResolver.ResolveAsync(ct);
            return Res.Ok(await _historyProvider.DeleteSessionAsync(
                partition,
                sessionId,
                expectedRevision,
                ct));
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    /// <summary>Clears all persisted sessions when the expected catalog revision is current.</summary>
    public async Task<Res<ChatHistoryWriteResult>> ClearAsync(
        long expectedRevision,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
            var partition = await _partitionResolver.ResolveAsync(ct);
            return Res.Ok(await _historyProvider.ClearAsync(partition, expectedRevision, ct));
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    /// <summary>Persists the current-session selection using optimistic concurrency.</summary>
    public async Task<Res<ChatHistoryWriteResult>> SetCurrentSessionAsync(
        string? sessionId,
        long expectedRevision,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
            var partition = await _partitionResolver.ResolveAsync(ct);
            return Res.Ok(await _historyProvider.SetCurrentSessionAsync(
                partition,
                string.IsNullOrWhiteSpace(sessionId) ? null : sessionId,
                expectedRevision,
                ct));
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }
}
