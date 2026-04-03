using Monica.UI.Shell.State;

namespace Monica.UI.Shell.Support;

/// <summary>
/// Convenience extension methods for common browser storage patterns
/// </summary>
public static class BrowserStorageExtensions
{
    private const string TableCategory = "table";

    /// <summary>
    /// Get persisted table state for a given table ID
    /// </summary>
    public static Task<TablePersistenceState> GetTableStateAsync(
        this IBrowserStorage storage,
        string tableId,
        int defaultPageSize = 10)
    {
        return storage.GetAsync(
            $"{TableCategory}:{tableId}",
            new TablePersistenceState(null, false, defaultPageSize));
    }

    /// <summary>
    /// Save table state for a given table ID
    /// </summary>
    public static Task SaveTableStateAsync(
        this IBrowserStorage storage,
        string tableId,
        TablePersistenceState state)
    {
        return storage.SetAsync($"{TableCategory}:{tableId}", state);
    }

    /// <summary>
    /// Clear all persisted table states
    /// </summary>
    public static Task<int> ClearAllTableStatesAsync(this IBrowserStorage storage)
    {
        return storage.ClearCategoryAsync(TableCategory);
    }
}
