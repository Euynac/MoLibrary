namespace Monica.DevOps.Git.Models;

/// <summary>
/// Well-known progress stage identifiers for Git repository operations.
/// </summary>
public static class GitRepositoryProgressStages
{
    public const string Preparing = nameof(Preparing);
    public const string Cloning = nameof(Cloning);
    public const string Fetching = nameof(Fetching);
    public const string Checkout = nameof(Checkout);
    public const string Deleting = nameof(Deleting);
}
