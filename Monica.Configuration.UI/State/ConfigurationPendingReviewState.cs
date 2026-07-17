using Monica.Configuration.Models;
using Monica.Configuration.UI.Models;

namespace Monica.Configuration.UI.State;

/// <summary>
/// Owns the mutable, dialog-local snapshot displayed by pending-review surfaces.
/// </summary>
public sealed class ConfigurationPendingReviewState
{
    private readonly Dictionary<PendingItemKey, PendingChange> _changes = [];
    private readonly Dictionary<PendingItemKey, ConfigurationValidationIssue> _validationIssues = [];

    /// <summary>
    /// Initializes a pending-review snapshot.
    /// </summary>
    /// <param name="changes">The staged changes to review.</param>
    /// <param name="validationIssues">The validation issues to review.</param>
    public ConfigurationPendingReviewState(
        IEnumerable<PendingChange> changes,
        IEnumerable<ConfigurationValidationIssue> validationIssues)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(validationIssues);

        foreach (var change in changes)
        {
            _changes[PendingItemKey.For(change.DefinitionKey, change.LogicalPath)] = change;
        }

        foreach (var issue in validationIssues)
        {
            _validationIssues[PendingItemKey.For(issue.DefinitionKey, issue.LogicalPath)] = issue;
        }
    }

    /// <summary>
    /// Gets the active staged changes in display order.
    /// </summary>
    public IReadOnlyList<PendingChange> Changes => _changes.Values
        .OrderBy(change => change.DefinitionDisplayName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(change => change.LogicalPath.ToCanonicalString(), StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Gets the active validation issues in display order.
    /// </summary>
    public IReadOnlyList<ConfigurationValidationIssue> ValidationIssues => _validationIssues.Values
        .OrderBy(issue => issue.DefinitionDisplayName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(issue => issue.LogicalPath.ToCanonicalString(), StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Gets the active review items grouped by definition.
    /// </summary>
    public IReadOnlyList<ConfigurationPendingReviewDefinition> Definitions
    {
        get
        {
            var definitionKeys = _changes.Values.Select(change => change.DefinitionKey)
                .Concat(_validationIssues.Values.Select(issue => issue.DefinitionKey))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            return definitionKeys
                .Select(BuildDefinition)
                .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    /// <summary>
    /// Gets whether any staged change or validation issue remains in the review.
    /// </summary>
    public bool HasItems => _changes.Count > 0 || _validationIssues.Count > 0;

    /// <summary>
    /// Removes the item or definition targeted by a review request.
    /// </summary>
    /// <param name="request">The removal request.</param>
    /// <returns><see langword="true"/> when the review state changed.</returns>
    public bool Undo(ConfigurationPendingReviewUndoRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Target switch
        {
            ConfigurationPendingReviewUndoTarget.Change => RemoveChange(request),
            ConfigurationPendingReviewUndoTarget.ValidationIssue => RemoveValidationIssue(request),
            ConfigurationPendingReviewUndoTarget.Definition => RemoveDefinition(request.DefinitionKey),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
    }

    /// <summary>
    /// Produces an import report that contains only items still included in this review.
    /// Definitions with no remaining state are omitted so applying the report cannot clear existing staged state.
    /// </summary>
    /// <param name="report">The original analyzed import report.</param>
    /// <returns>A filtered report suitable for applying to the staging store.</returns>
    public ConfigurationImportReport Filter(ConfigurationImportReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return report with
        {
            Drafts = report.Drafts
                .Select(FilterDraft)
                .Where(draft => draft.HasState)
                .ToArray()
        };
    }

    private ConfigurationPendingReviewDefinition BuildDefinition(string definitionKey)
    {
        var changes = _changes.Values
            .Where(change => MatchesDefinition(change.DefinitionKey, definitionKey))
            .OrderBy(change => change.LogicalPath.ToCanonicalString(), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var issues = _validationIssues.Values
            .Where(issue => MatchesDefinition(issue.DefinitionKey, definitionKey))
            .OrderBy(issue => issue.LogicalPath.ToCanonicalString(), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var displayName = changes.FirstOrDefault()?.DefinitionDisplayName
                          ?? issues.FirstOrDefault()?.DefinitionDisplayName
                          ?? definitionKey;

        return new ConfigurationPendingReviewDefinition(definitionKey, displayName, changes, issues);
    }

    private bool RemoveChange(ConfigurationPendingReviewUndoRequest request)
    {
        return _changes.Remove(PendingItemKey.For(request.DefinitionKey, request.LogicalPath!));
    }

    private bool RemoveValidationIssue(ConfigurationPendingReviewUndoRequest request)
    {
        return _validationIssues.Remove(PendingItemKey.For(request.DefinitionKey, request.LogicalPath!));
    }

    private bool RemoveDefinition(string definitionKey)
    {
        var changeKeys = _changes.Keys
            .Where(key => MatchesDefinition(key.DefinitionKey, definitionKey))
            .ToArray();
        var issueKeys = _validationIssues.Keys
            .Where(key => MatchesDefinition(key.DefinitionKey, definitionKey))
            .ToArray();

        foreach (var key in changeKeys)
        {
            _changes.Remove(key);
        }

        foreach (var key in issueKeys)
        {
            _validationIssues.Remove(key);
        }

        return changeKeys.Length > 0 || issueKeys.Length > 0;
    }

    private ConfigurationJsonDraftResult FilterDraft(ConfigurationJsonDraftResult draft)
    {
        var changes = draft.Changes
            .Where(change => _changes.ContainsKey(PendingItemKey.For(change.DefinitionKey, change.LogicalPath)))
            .ToArray();
        var issues = draft.ValidationIssues
            .Where(issue => _validationIssues.ContainsKey(PendingItemKey.For(issue.DefinitionKey, issue.LogicalPath)))
            .ToArray();

        return draft with
        {
            Changes = changes,
            ValidationIssues = issues
        };
    }

    private static bool MatchesDefinition(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct PendingItemKey(string DefinitionKey, LogicalPath LogicalPath)
    {
        public static PendingItemKey For(string definitionKey, LogicalPath logicalPath)
        {
            return new PendingItemKey(definitionKey, logicalPath);
        }
    }
}

/// <summary>
/// Presents the pending review items owned by one configuration definition.
/// </summary>
/// <param name="DefinitionKey">The definition key.</param>
/// <param name="DisplayName">The operator-facing definition name.</param>
/// <param name="Changes">The active staged changes.</param>
/// <param name="ValidationIssues">The active validation issues.</param>
public sealed record ConfigurationPendingReviewDefinition(
    string DefinitionKey,
    string DisplayName,
    IReadOnlyList<PendingChange> Changes,
    IReadOnlyList<ConfigurationValidationIssue> ValidationIssues)
{
    /// <summary>
    /// Gets the total number of active review items.
    /// </summary>
    public int ItemCount => Changes.Count + ValidationIssues.Count;
}
