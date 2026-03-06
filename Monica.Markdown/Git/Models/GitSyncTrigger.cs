namespace Monica.Markdown.Git.Models;

/// <summary>
/// Describes the source that triggered a synchronization operation.
/// </summary>
public enum GitSyncTrigger
{
    Startup = 1,
    Webhook = 2,
    Manual = 3
}
