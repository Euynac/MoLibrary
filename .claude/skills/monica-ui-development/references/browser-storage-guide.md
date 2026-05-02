# Browser Storage Guide (`IBrowserStorage`)

## Overview

`IBrowserStorage` is the Monica UI service for persisting UI state in browser `localStorage` or `sessionStorage`. It provides a type-safe, JSON-serialized API with automatic key prefixing to avoid collisions.

**Service lifetime**: Scoped (one instance per Blazor circuit)

## Key Naming Convention

All keys are auto-prefixed with `mo:` by the service. Use the format `{category}:{id}`:

| You pass | Stored as | Example |
|----------|-----------|---------|
| `table:job-instances` | `mo:table:job-instances` | Table sort/page state |
| `theme:data` | `mo:theme:data` | Theme preferences |
| `settings:sidebar` | `mo:settings:sidebar` | Custom UI settings |

**Never include the `mo:` prefix yourself** — the service adds it automatically.

## Core API

```csharp
public interface IBrowserStorage : IAsyncDisposable
{
    // Get a value (returns defaultValue if key missing or on error)
    Task<T> GetAsync<T>(string key, T defaultValue,
        BrowserStorageType storageType = BrowserStorageType.Local);

    // Set a value (serialized as JSON)
    Task SetAsync<T>(string key, T value,
        BrowserStorageType storageType = BrowserStorageType.Local);

    // Remove a single key
    Task RemoveAsync(string key,
        BrowserStorageType storageType = BrowserStorageType.Local);

    // Get all keys matching a category prefix (e.g. "table" → "mo:table:*")
    Task<IReadOnlyList<string>> GetKeysAsync(string category,
        BrowserStorageType storageType = BrowserStorageType.Local);

    // Clear all keys in a category, returns count removed
    Task<int> ClearCategoryAsync(string category,
        BrowserStorageType storageType = BrowserStorageType.Local);
}
```

**Storage types**: `BrowserStorageType.Local` (default, persists across sessions) or `BrowserStorageType.Session` (cleared when tab closes).

## Table State Persistence Pattern

The most common use case. Uses `BrowserStorageExtensions` to persist sort column, sort direction, and page size for `MudTable` components.

### Model

```csharp
public record TablePersistenceState(string? SortBy, bool SortDescending, int PageSize);
```

### Extension Methods

```csharp
// Get persisted state (defaults: no sort, pageSize = 10)
Task<TablePersistenceState> GetTableStateAsync(string tableId, int defaultPageSize = 10)

// Save current state
Task SaveTableStateAsync(string tableId, TablePersistenceState state)

// Clear all persisted table states
Task<int> ClearAllTableStatesAsync()
```

### Complete Usage Example

```razor
@inject IBrowserStorage BrowserStorage

@* Deferred rendering: only render table after state is loaded *@
@if (_stateLoaded)
{
    <MudTable @ref="_table"
              ServerData="LoadDataAsync"
              CurrentPage="0"
              RowsPerPage="@_pageSize"
              SortLabel="@_sortBy"
              SortDirection="@(_sortDescending ? SortDirection.Descending : SortDirection.Ascending)">
        @* ... table columns ... *@
    </MudTable>
}

@code {
    private MudTable<MyItem>? _table;

    // Table state persistence fields
    private string? _sortBy = "CreatedAt";
    private bool _sortDescending = true;
    private int _pageSize = 10;
    private bool _stateLoaded = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Load persisted state from browser storage
            var state = await BrowserStorage.GetTableStateAsync("my-table-id");
            if (!string.IsNullOrEmpty(state.SortBy))
            {
                _sortBy = state.SortBy;
                _sortDescending = state.SortDescending;
            }
            _pageSize = state.PageSize;
            _stateLoaded = true;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task<TableData<MyItem>> LoadDataAsync(TableState state, CancellationToken ct)
    {
        // Update local state from MudTable's current state
        if (!string.IsNullOrEmpty(state.SortLabel))
        {
            _sortBy = state.SortLabel;
            _sortDescending = state.SortDirection == SortDirection.Descending;
        }
        _pageSize = state.PageSize;

        // Persist to browser storage
        await BrowserStorage.SaveTableStateAsync("my-table-id", new TablePersistenceState(
            SortBy: _sortBy,
            SortDescending: _sortDescending,
            PageSize: _pageSize
        ));

        // ... fetch and return data ...
    }
}
```

### Key Points

1. **Deferred rendering**: Wrap the table in `@if (_stateLoaded)` to prevent rendering with default values before persisted state loads
2. **Load in `OnAfterRenderAsync`**: Browser storage requires JS interop, which only works after first render
3. **Save on every data load**: Persist state in the `ServerData` callback so changes are captured immediately
4. **Use a unique table ID**: Each table needs a distinct ID string (e.g. `"job-instances"`, `"user-list"`)

## Theme Persistence Pattern

```razor
@inject IBrowserStorage BrowserStorage

@code {
    private record ThemeData(string ThemeName, bool IsDarkMode);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Load persisted theme
            var themeData = await BrowserStorage.GetAsync<ThemeData?>("theme:data", null);

            await InvokeAsync(() =>
            {
                if (themeData != null)
                {
                    ThemeService.CurrentThemeName = themeData.ThemeName;
                    ThemeService.IsDarkMode = themeData.IsDarkMode;
                }
            });
        }
    }

    private async void OnThemeChanged()
    {
        // Save theme preference
        await BrowserStorage.SetAsync("theme:data", new ThemeData(
            ThemeService.CurrentThemeName, ThemeService.IsDarkMode));

        await InvokeAsync(StateHasChanged);
    }
}
```

## Adding Custom State Persistence

To persist a new category of UI state:

### 1. Define a model

```csharp
public record SidebarPersistenceState(bool IsCollapsed, int Width);
```

### 2. Add extension methods

```csharp
public static class BrowserStorageExtensions
{
    private const string SidebarCategory = "sidebar";

    public static Task<SidebarPersistenceState> GetSidebarStateAsync(
        this IBrowserStorage storage, string sidebarId)
    {
        return storage.GetAsync(
            $"{SidebarCategory}:{sidebarId}",
            new SidebarPersistenceState(false, 280));
    }

    public static Task SaveSidebarStateAsync(
        this IBrowserStorage storage, string sidebarId, SidebarPersistenceState state)
    {
        return storage.SetAsync($"{SidebarCategory}:{sidebarId}", state);
    }
}
```

### 3. Use in component

Follow the same deferred rendering pattern as table state persistence.

## Critical Rules

1. **Always use `IBrowserStorage`** — never use raw `IJSRuntime` calls for localStorage/sessionStorage
2. **Always load state in `OnAfterRenderAsync`** — JS interop is not available during static rendering
3. **Use deferred rendering** — wrap state-dependent UI in `@if (_stateLoaded)` to prevent flash of default values
4. **Handle `JSDisconnectedException`** — the service handles this internally, but if you use `OnThemeChanged`-style `async void` handlers, wrap calls in try/catch
5. **Use category-based keys** — follow the `{category}:{id}` convention for organized storage
6. **Manual debugging uses prefixed keys** — application code passes `theme:data`, but Playwright or DevTools must write `mo:theme:data` because the service adds the prefix automatically
7. **`ThemeState` is Scoped** — not Singleton, because it needs per-circuit state in Blazor Server
