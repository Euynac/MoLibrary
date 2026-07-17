using Monica.Configuration.Models;

namespace Monica.Configuration.UI.State;

/// <summary>
/// Describes one removal from a pending-review surface.
/// </summary>
public sealed record ConfigurationPendingReviewUndoRequest
{
    private ConfigurationPendingReviewUndoRequest(
        ConfigurationPendingReviewUndoTarget target,
        string definitionKey,
        LogicalPath? logicalPath)
    {
        Target = target;
        DefinitionKey = definitionKey;
        LogicalPath = logicalPath;
    }

    /// <summary>
    /// Gets the kind of pending state to remove.
    /// </summary>
    public ConfigurationPendingReviewUndoTarget Target { get; }

    /// <summary>
    /// Gets the definition that owns the pending state.
    /// </summary>
    public string DefinitionKey { get; }

    /// <summary>
    /// Gets the item path, or <see langword="null"/> when the whole definition is targeted.
    /// </summary>
    public LogicalPath? LogicalPath { get; }

    /// <summary>
    /// Creates a request for one staged change.
    /// </summary>
    /// <param name="change">The staged change to remove.</param>
    /// <returns>A path-level request.</returns>
    public static ConfigurationPendingReviewUndoRequest ForChange(PendingChange change)
    {
        return new ConfigurationPendingReviewUndoRequest(
            ConfigurationPendingReviewUndoTarget.Change,
            change.DefinitionKey,
            change.LogicalPath);
    }

    /// <summary>
    /// Creates a request for one validation issue.
    /// </summary>
    /// <param name="issue">The validation issue to remove.</param>
    /// <returns>A path-level request.</returns>
    public static ConfigurationPendingReviewUndoRequest ForValidationIssue(ConfigurationValidationIssue issue)
    {
        return new ConfigurationPendingReviewUndoRequest(
            ConfigurationPendingReviewUndoTarget.ValidationIssue,
            issue.DefinitionKey,
            issue.LogicalPath);
    }

    /// <summary>
    /// Creates a request that removes all staged changes and issues for one definition.
    /// </summary>
    /// <param name="definitionKey">The definition to remove from the review.</param>
    /// <returns>A definition-level request.</returns>
    public static ConfigurationPendingReviewUndoRequest ForDefinition(string definitionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        return new ConfigurationPendingReviewUndoRequest(
            ConfigurationPendingReviewUndoTarget.Definition,
            definitionKey,
            null);
    }

    /// <summary>
    /// Applies this request to the shared staging store.
    /// </summary>
    /// <param name="store">The staging store to mutate.</param>
    public void ApplyTo(ConfigurationStateStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        switch (Target)
        {
            case ConfigurationPendingReviewUndoTarget.Change:
                store.UndoChange(DefinitionKey, LogicalPath!);
                break;
            case ConfigurationPendingReviewUndoTarget.ValidationIssue:
                store.ClearValidationIssue(DefinitionKey, LogicalPath!);
                break;
            case ConfigurationPendingReviewUndoTarget.Definition:
                store.UndoScope(DefinitionKey, LogicalPath.Root);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Target));
        }
    }
}

/// <summary>
/// Identifies the pending state targeted by a review removal request.
/// </summary>
public enum ConfigurationPendingReviewUndoTarget
{
    /// <summary>
    /// One staged change.
    /// </summary>
    Change,

    /// <summary>
    /// One validation issue.
    /// </summary>
    ValidationIssue,

    /// <summary>
    /// Every staged change and issue owned by one definition.
    /// </summary>
    Definition
}

/// <summary>
/// Selects the language and icon used for pending-review removal actions.
/// </summary>
public enum ConfigurationPendingReviewActionMode
{
    /// <summary>
    /// Items are already staged, so removal is presented as undo.
    /// </summary>
    Undo,

    /// <summary>
    /// Items have not been staged yet, so removal is presented as exclusion.
    /// </summary>
    Exclude
}
