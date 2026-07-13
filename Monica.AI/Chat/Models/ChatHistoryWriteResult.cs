namespace Monica.AI.Chat.Models;

/// <summary>Outcome category for a revision-aware chat history write.</summary>
public enum ChatHistoryWriteStatus
{
    /// <summary>The provider durably applied the requested mutation.</summary>
    Succeeded,

    /// <summary>The expected revision was stale and no mutation was applied.</summary>
    Conflict,

    /// <summary>Persistence is disabled, so the mutation remains in memory only.</summary>
    NotPersisted
}

/// <summary>Provider-neutral reason why a chat history mutation was not persisted.</summary>
public enum ChatHistoryWriteFailureReason
{
    /// <summary>The mutation succeeded and has no failure reason.</summary>
    None,

    /// <summary>Persistence was not configured for the application.</summary>
    PersistenceDisabled,

    /// <summary>The expected revision was stale.</summary>
    Conflict,

    /// <summary>The persistence medium did not have enough capacity.</summary>
    QuotaExceeded,

    /// <summary>The persistence medium was unavailable.</summary>
    StorageUnavailable,

    /// <summary>The snapshot or provider metadata could not be serialized.</summary>
    SerializationFailed,

    /// <summary>Persisted provider metadata was missing, inconsistent, or corrupt.</summary>
    CorruptData,

    /// <summary>The provider could not classify the failure more precisely.</summary>
    Unknown
}

/// <summary>Immutable result returned by chat history providers after a mutation.</summary>
public sealed record ChatHistoryWriteResult
{
    private IReadOnlyList<string> _prunedSessionIds = Array.Empty<string>();

    /// <summary>Current serialized contract version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Contract version used to create this result.</summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>Mutation outcome.</summary>
    public required ChatHistoryWriteStatus Status { get; init; }

    /// <summary>Current catalog revision after the attempted mutation.</summary>
    public long Revision { get; init; }

    /// <summary>Sessions pruned by provider retention policy while applying the mutation.</summary>
    public IReadOnlyList<string> PrunedSessionIds
    {
        get => _prunedSessionIds;
        init => _prunedSessionIds = Array.AsReadOnly(value.ToArray());
    }

    /// <summary>
    /// Structured provider-neutral reason when the mutation was not persisted, or
    /// <see cref="ChatHistoryWriteFailureReason.None"/> after success.
    /// </summary>
    public ChatHistoryWriteFailureReason FailureReason { get; init; }

    /// <summary>Optional provider warning suitable for diagnostic presentation.</summary>
    public string? Warning { get; init; }

    /// <summary>Whether the requested mutation was durably applied.</summary>
    public bool IsPersisted => Status == ChatHistoryWriteStatus.Succeeded;

    /// <summary>Whether the provider rejected the mutation because its revision was stale.</summary>
    public bool IsConflict => Status == ChatHistoryWriteStatus.Conflict;

    /// <summary>Creates the result used when persistence was not configured.</summary>
    public static ChatHistoryWriteResult PersistenceDisabled { get; } = new()
    {
        Status = ChatHistoryWriteStatus.NotPersisted,
        FailureReason = ChatHistoryWriteFailureReason.PersistenceDisabled,
        Warning = "Chat history persistence is not configured."
    };
}
