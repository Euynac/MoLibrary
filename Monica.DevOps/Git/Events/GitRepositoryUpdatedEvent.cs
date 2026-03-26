using Monica.EventBus.Attributes;
using Monica.DevOps.Git.Models;

namespace Monica.DevOps.Git.Events;

/// <summary>
/// Published after a repository finishes a successful synchronization.
/// </summary>
[EventName("git.repository.updated")]
public sealed record GitRepositoryUpdatedEvent(
    string RepositoryId,
    GitSyncTrigger Trigger,
    string? PreviousCommit,
    string? CurrentCommit,
    bool HasChanges,
    DateTimeOffset SyncedAtUtc,
    string? Provider,
    string? Branch);
