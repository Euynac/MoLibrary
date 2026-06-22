using Monica.Configuration.Models;
using Monica.Configuration.UI.State;

namespace Monica.Configuration.UI.Models;

/// <summary>
/// Represents the action chosen from the save mutation group dialog.
/// </summary>
public sealed record SaveMutationGroupDialogResult
{
    /// <summary>
    /// Gets the saved mutation group when the dialog completed a save.
    /// </summary>
    public ConfigurationMutationGroup? SavedGroup { get; init; }

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
    /// <param name="group">The persisted mutation group.</param>
    /// <returns>The dialog result.</returns>
    public static SaveMutationGroupDialogResult Saved(ConfigurationMutationGroup group)
    {
        return new SaveMutationGroupDialogResult
        {
            SavedGroup = group
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
