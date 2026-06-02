using Monica.Configuration.Models;

namespace Monica.Configuration.UI.State;

/// <summary>
/// Scoped UI store for staged configuration mutations.
/// </summary>
public sealed class ConfigurationStateStore
{
    private readonly Dictionary<(string DefinitionKey, LogicalPath LogicalPath), PendingChange> _pendingChanges = [];
    private readonly Dictionary<(string DefinitionKey, LogicalPath LogicalPath), ConfigurationValidationIssue> _validationIssues = [];

    /// <summary>
    /// Raised whenever staged changes are modified.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Gets all staged changes.
    /// </summary>
    /// <returns>Pending changes ordered for display.</returns>
    public IReadOnlyList<PendingChange> GetPending()
    {
        return _pendingChanges.Values
            .OrderBy(change => change.DefinitionDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(change => change.LogicalPath.ToCanonicalString(), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Gets all active validation issues.
    /// </summary>
    /// <returns>Validation issues ordered for display.</returns>
    public IReadOnlyList<ConfigurationValidationIssue> GetValidationIssues()
    {
        return _validationIssues.Values
            .OrderBy(issue => issue.DefinitionDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.LogicalPath.ToCanonicalString(), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Gets a staged change for one path.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="path">The logical path.</param>
    /// <returns>The staged change, or null when none exists.</returns>
    public PendingChange? Get(string definitionKey, LogicalPath path)
    {
        return _pendingChanges.GetValueOrDefault(Key(definitionKey, path));
    }

    /// <summary>
    /// Gets a validation issue for one path.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="path">The logical path.</param>
    /// <returns>The validation issue, or null when none exists.</returns>
    public ConfigurationValidationIssue? GetValidationIssue(string definitionKey, LogicalPath path)
    {
        return _validationIssues.GetValueOrDefault(Key(definitionKey, path));
    }

    /// <summary>
    /// Gets all staged changes inside one definition path scope.
    /// </summary>
    /// <param name="definitionKey">The definition key that owns the scope.</param>
    /// <param name="scopePath">The root logical path of the scope.</param>
    /// <returns>The staged changes under the scope.</returns>
    public IReadOnlyList<PendingChange> GetScope(string definitionKey, LogicalPath scopePath)
    {
        return _pendingChanges.Values
            .Where(change => string.Equals(change.DefinitionKey, definitionKey, StringComparison.Ordinal) && IsPrefix(scopePath, change.LogicalPath))
            .OrderBy(change => change.LogicalPath.ToCanonicalString(), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Stages one pending change.
    /// </summary>
    /// <param name="change">The pending change.</param>
    public void Stage(PendingChange change)
    {
        _pendingChanges[Key(change.DefinitionKey, change.LogicalPath)] = change;
        _validationIssues.Remove(Key(change.DefinitionKey, change.LogicalPath));
        NotifyChanged();
    }

    /// <summary>
    /// Reports one invalid UI edit.
    /// </summary>
    /// <param name="issue">The validation issue.</param>
    public void ReportValidationIssue(ConfigurationValidationIssue issue)
    {
        _validationIssues[Key(issue.DefinitionKey, issue.LogicalPath)] = issue;
        _pendingChanges.Remove(Key(issue.DefinitionKey, issue.LogicalPath));
        NotifyChanged();
    }

    /// <summary>
    /// Clears one validation issue.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="path">The logical path.</param>
    public void ClearValidationIssue(string definitionKey, LogicalPath path)
    {
        if (_validationIssues.Remove(Key(definitionKey, path)))
        {
            NotifyChanged();
        }
    }

    /// <summary>
    /// Replaces all staged changes inside one definition path scope.
    /// </summary>
    /// <param name="definitionKey">The definition key that owns the scope.</param>
    /// <param name="scopePath">The root logical path of the scope to replace.</param>
    /// <param name="changes">The replacement staged changes for the scope.</param>
    public void ReplaceScope(string definitionKey, LogicalPath scopePath, IReadOnlyList<PendingChange> changes)
    {
        var scopedKeys = _pendingChanges.Keys
            .Where(key => string.Equals(key.DefinitionKey, definitionKey, StringComparison.Ordinal) && IsPrefix(scopePath, key.LogicalPath))
            .ToArray();
        var scopedIssueKeys = _validationIssues.Keys
            .Where(key => string.Equals(key.DefinitionKey, definitionKey, StringComparison.Ordinal) && IsPrefix(scopePath, key.LogicalPath))
            .ToArray();

        foreach (var key in scopedKeys)
        {
            _pendingChanges.Remove(key);
        }

        foreach (var key in scopedIssueKeys)
        {
            _validationIssues.Remove(key);
        }

        foreach (var change in changes.Where(change =>
                     string.Equals(change.DefinitionKey, definitionKey, StringComparison.Ordinal) && IsPrefix(scopePath, change.LogicalPath)))
        {
            _pendingChanges[Key(change.DefinitionKey, change.LogicalPath)] = change;
        }

        if (scopedKeys.Length > 0 || scopedIssueKeys.Length > 0 || changes.Count > 0)
        {
            NotifyChanged();
        }
    }

    /// <summary>
    /// Removes one pending change.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="path">The logical path.</param>
    public void Undo(string definitionKey, LogicalPath path)
    {
        var key = Key(definitionKey, path);
        var removedPending = _pendingChanges.Remove(key);
        var removedIssue = _validationIssues.Remove(key);
        if (removedPending || removedIssue)
        {
            NotifyChanged();
        }
    }

    /// <summary>
    /// Removes all staged changes inside one definition path scope.
    /// </summary>
    /// <param name="definitionKey">The definition key that owns the scope.</param>
    /// <param name="scopePath">The root logical path of the scope.</param>
    public void UndoScope(string definitionKey, LogicalPath scopePath)
    {
        var scopedKeys = _pendingChanges.Keys
            .Where(key => string.Equals(key.DefinitionKey, definitionKey, StringComparison.Ordinal) && IsPrefix(scopePath, key.LogicalPath))
            .ToArray();
        var scopedIssueKeys = _validationIssues.Keys
            .Where(key => string.Equals(key.DefinitionKey, definitionKey, StringComparison.Ordinal) && IsPrefix(scopePath, key.LogicalPath))
            .ToArray();

        foreach (var key in scopedKeys)
        {
            _pendingChanges.Remove(key);
        }

        foreach (var key in scopedIssueKeys)
        {
            _validationIssues.Remove(key);
        }

        if (scopedKeys.Length > 0 || scopedIssueKeys.Length > 0)
        {
            NotifyChanged();
        }
    }

    /// <summary>
    /// Clears all pending changes.
    /// </summary>
    public void Clear()
    {
        if (_pendingChanges.Count == 0 && _validationIssues.Count == 0)
        {
            return;
        }

        _pendingChanges.Clear();
        _validationIssues.Clear();
        NotifyChanged();
    }

    private static (string DefinitionKey, LogicalPath LogicalPath) Key(string definitionKey, LogicalPath path)
    {
        return (definitionKey, path);
    }

    private static bool IsPrefix(LogicalPath ancestor, LogicalPath path)
    {
        if (ancestor.Depth > path.Depth)
        {
            return false;
        }

        for (var index = 0; index < ancestor.Depth; index++)
        {
            if (!ancestor.Segments[index].Equals(path.Segments[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
