using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Models;

/// <summary>
/// Describes a successfully persisted history rollback returned by the confirmation dialog.
/// </summary>
public sealed record ConfigurationHistoryRollbackDialogResult
{
    /// <summary>
    /// Gets the individual mutations persisted by the rollback operation.
    /// </summary>
    public required IReadOnlyList<ConfigurationMutationResult> AppliedMutations { get; init; }

    /// <summary>
    /// Gets distinct failures reported by reload, notification, audit, or verification work after persistence.
    /// </summary>
    public required IReadOnlyList<ConfigurationPostCommitIssue> PostCommitIssues { get; init; }

    /// <summary>
    /// Creates a dialog result from one persisted mutation.
    /// </summary>
    /// <param name="mutation">The persisted rollback mutation.</param>
    /// <returns>The normalized dialog result.</returns>
    public static ConfigurationHistoryRollbackDialogResult Applied(ConfigurationMutationResult mutation)
    {
        return Applied([mutation]);
    }

    /// <summary>
    /// Creates a dialog result from the mutations persisted by one rollback operation.
    /// </summary>
    /// <param name="mutations">The persisted rollback mutations.</param>
    /// <returns>The normalized dialog result.</returns>
    public static ConfigurationHistoryRollbackDialogResult Applied(
        IReadOnlyList<ConfigurationMutationResult> mutations)
    {
        var appliedMutations = mutations.ToArray();
        return new ConfigurationHistoryRollbackDialogResult
        {
            AppliedMutations = appliedMutations,
            PostCommitIssues = appliedMutations
                .SelectMany(static mutation => mutation.PostCommitIssues)
                .Distinct()
                .ToArray()
        };
    }
}
