using System.Text.Json;
using Microsoft.JSInterop;

namespace MoLibrary.JobScheduler.UI.Services;

/// <summary>
/// Reusable helper for managing MudTable state persistence via localStorage
/// </summary>
public class TableStateHelper(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private bool _moduleLoaded;

    private async Task EnsureModuleLoadedAsync()
    {
        if (_moduleLoaded) return;

        _module = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/MoLibrary.JobScheduler.UI/js/table-state-helper.js"
        );
        _moduleLoaded = true;
    }

    public async Task<TablePersistenceState> LoadStateAsync(
        string stateKey,
        int[]? allowedPageSizes = null)
    {
        await EnsureModuleLoadedAsync();

        var defaultPageSizes = allowedPageSizes ?? [10, 20, 50, 100];
        var defaultState = new TablePersistenceState(
            SortBy: null,
            SortDescending: false,
            PageSize: defaultPageSizes[0]
        );

        try
        {
            if (_module == null) return defaultState;

            var stateJson = await _module.InvokeAsync<JsonElement?>("getTableState", stateKey);

            if (!stateJson.HasValue || stateJson.Value.ValueKind != JsonValueKind.Object)
                return defaultState;

            var state = stateJson.Value;

            string? sortBy = null;
            if (state.TryGetProperty("sortBy", out var sortByProp) &&
                sortByProp.ValueKind == JsonValueKind.String)
            {
                sortBy = sortByProp.GetString();
            }

            bool sortDescending = false;
            if (state.TryGetProperty("sortDirection", out var sortDirProp) &&
                sortDirProp.ValueKind == JsonValueKind.String)
            {
                sortDescending = sortDirProp.GetString() == "descending";
            }

            int pageSize = defaultPageSizes[0];
            if (state.TryGetProperty("pageSize", out var pageSizeProp) &&
                pageSizeProp.ValueKind == JsonValueKind.Number)
            {
                var size = pageSizeProp.GetInt32();
                if (defaultPageSizes.Contains(size))
                {
                    pageSize = size;
                }
            }

            return new TablePersistenceState(sortBy, sortDescending, pageSize);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load table state for '{stateKey}': {ex.Message}");
            return defaultState;
        }
    }

    public async Task SaveStateAsync(string stateKey, TablePersistenceState state)
    {
        await EnsureModuleLoadedAsync();

        try
        {
            if (_module == null) return;

            await _module.InvokeVoidAsync("saveTableState", stateKey, new
            {
                sortBy = state.SortBy,
                sortDirection = state.SortDescending ? "descending" : "ascending",
                pageSize = state.PageSize
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save table state for '{stateKey}': {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}

public record TablePersistenceState(
    string? SortBy,
    bool SortDescending,
    int PageSize
);
