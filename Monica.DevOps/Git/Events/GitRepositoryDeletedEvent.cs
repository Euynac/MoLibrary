using Monica.EventBus.Attributes;

namespace Monica.DevOps.Git.Events;

/// <summary>
/// Published after a repository working copy is deleted successfully.
/// </summary>
[EventName("git.repository.deleted")]
public sealed record GitRepositoryDeletedEvent(
    string RepositoryId,
    string ResolvedLocalPath,
    bool PathExisted,
    DateTimeOffset DeletedAtUtc);
