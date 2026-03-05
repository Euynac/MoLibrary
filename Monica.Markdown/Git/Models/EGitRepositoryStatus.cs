namespace Monica.Markdown.Git.Models;

/// <summary>
/// Runtime status of a tracked Git repository.
/// </summary>
public enum EGitRepositoryStatus
{
    /// <summary>
    /// Status has not been determined yet.
    /// </summary>
    Unknown,

    /// <summary>
    /// Repository is being cloned.
    /// </summary>
    Cloning,

    /// <summary>
    /// Repository is being pulled.
    /// </summary>
    Pulling,

    /// <summary>
    /// Repository is cloned and up to date.
    /// </summary>
    Ready,

    /// <summary>
    /// An error occurred during a Git operation.
    /// </summary>
    Error
}
