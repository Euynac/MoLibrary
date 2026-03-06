using Monica.EventBus.Attributes;
using Monica.Markdown.Git.Models;

namespace Monica.Markdown.Git.Events;

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
    IReadOnlyList<string> BoundDocumentGroupKeys,
    string? Provider,
    string? Branch);
