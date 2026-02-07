namespace Monica.UI.Services.Models;

/// <summary>
/// Persisted state for MudTable components (sort, page size)
/// </summary>
public record TablePersistenceState(string? SortBy, bool SortDescending, int PageSize);
