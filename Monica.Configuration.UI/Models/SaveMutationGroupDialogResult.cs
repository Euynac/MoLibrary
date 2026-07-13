using Monica.Configuration.Models;
using Monica.Configuration.UI.State;

namespace Monica.Configuration.UI.Models;

/// <summary>
/// Represents the action chosen from the save mutation group dialog.
/// </summary>
public sealed record SaveMutationGroupDialogResult
{
    /// <summary>
    /// Gets the mutation-group application result when persistence was attempted successfully.
    /// </summary>
    public ConfigurationMutationGroupApplyResult? ApplyResult { get; init; }

    /// <summary>
    /// Gets the concurrency conflict that requires staged baselines to be refreshed.
    /// </summary>
    public string? ConcurrencyConflictMessage { get; init; }

    /// <summary>
    /// Gets the validation issue the operator wants to edit.
    /// </summary>
    public ConfigurationValidationIssue? JumpTarget { get; init; }

    /// <summary>
    /// Gets the staged change the operator wants to edit.
    /// </summary>
    public PendingChange? JumpChangeTarget { get; init; }

    /// <summary>
    /// Creates a saved-group result.
    /// </summary>
    /// <param name="result">The mutation-group result.</param>
    /// <returns>The dialog result.</returns>
    public static SaveMutationGroupDialogResult Saved(ConfigurationMutationGroupApplyResult result)
    {
        return new SaveMutationGroupDialogResult
        {
            ApplyResult = result
        };
    }

    /// <summary>
    /// Creates a result that asks the page to refresh staged concurrency baselines.
    /// </summary>
    /// <param name="message">Conflict diagnostic.</param>
    /// <returns>The dialog result.</returns>
    public static SaveMutationGroupDialogResult ConcurrencyConflict(string message)
    {
        return new SaveMutationGroupDialogResult
        {
            ConcurrencyConflictMessage = message
        };
    }

    /// <summary>
    /// Creates a jump-to-validation-issue result.
    /// </summary>
    /// <param name="issue">The issue to jump to.</param>
    /// <returns>The dialog result.</returns>
    public static SaveMutationGroupDialogResult JumpTo(ConfigurationValidationIssue issue)
    {
        return new SaveMutationGroupDialogResult
        {
            JumpTarget = issue
        };
    }

    /// <summary>
    /// Creates a jump-to-staged-change result.
    /// </summary>
    /// <param name="change">The staged change to jump to.</param>
    /// <returns>The dialog result.</returns>
    public static SaveMutationGroupDialogResult JumpTo(PendingChange change)
    {
        return new SaveMutationGroupDialogResult
        {
            JumpChangeTarget = change
        };
    }
}
