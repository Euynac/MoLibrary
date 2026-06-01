using Monica.Configuration.Models;

namespace Monica.Configuration.UI.State;

/// <summary>
/// Scoped UI store for staged configuration mutations.
/// </summary>
public sealed class ConfigurationStateStore
{
    private readonly Dictionary<(string DefinitionKey, LogicalPath LogicalPath), PendingChange> _pendingChanges = [];

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
    /// Stages one pending change.
    /// </summary>
    /// <param name="change">The pending change.</param>
    public void Stage(PendingChange change)
    {
        _pendingChanges[Key(change.DefinitionKey, change.LogicalPath)] = change;
        NotifyChanged();
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

        foreach (var key in scopedKeys)
        {
            _pendingChanges.Remove(key);
        }

        foreach (var change in changes.Where(change =>
                     string.Equals(change.DefinitionKey, definitionKey, StringComparison.Ordinal) && IsPrefix(scopePath, change.LogicalPath)))
        {
            _pendingChanges[Key(change.DefinitionKey, change.LogicalPath)] = change;
        }

        if (scopedKeys.Length > 0 || changes.Count > 0)
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
        if (_pendingChanges.Remove(Key(definitionKey, path)))
        {
            NotifyChanged();
        }
    }

    /// <summary>
    /// Clears all pending changes.
    /// </summary>
    public void Clear()
    {
        if (_pendingChanges.Count == 0)
        {
            return;
        }

        _pendingChanges.Clear();
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
