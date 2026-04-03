namespace Monica.UI.Shell.State;

/// <summary>
/// Persisted state for MudTable components (sort, page size)
/// </summary>
public record TablePersistenceState(string? SortBy, bool SortDescending, int PageSize);
