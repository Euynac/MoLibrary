using Microsoft.Extensions.Options;
using Monica.Core.Results;
using Monica.Modules;
using Monica.Profiling.MemoryDiagnostics.Facades;
using Monica.Profiling.MemoryDiagnostics.Models;
using Monica.Profiling.RuntimeMetrics.Facades;
using Monica.Profiling.RuntimeMetrics.Models;

namespace Monica.Profiling.UIMemoryAnalysis.State;

public sealed class MemoryAnalysisPageState(
    MemoryDiagnosticsFacade memoryDiagnosticsFacade,
    RuntimeMetricsFacade runtimeMetricsFacade,
    IOptions<ModuleMemoryAnalysisUIOption> options)
    : IDisposable
{
    private readonly ModuleMemoryAnalysisUIOption _option = options.Value;
    private Timer? _refreshTimer;

    public event Action? StateChanged;

    public MemorySnapshot? CurrentSnapshot { get; private set; }

    public GcDetails? CurrentGcDetails { get; private set; }

    public RuntimeMetricsTrend? TrendData { get; private set; }

    public bool IsLoading { get; private set; }

    public bool AutoRefresh { get; private set; } = true;

    public int ActiveTabIndex { get; set; }

    public bool IsTypeAllocationTabEnabled => _option.EnableTypeAllocationTab;

    public async Task<Res> InitializeAsync()
    {
        var result = await RefreshAsync();
        UpdateAutoRefresh();
        return result;
    }

    public void SetAutoRefresh(bool value)
    {
        if (AutoRefresh == value)
        {
            return;
        }

        AutoRefresh = value;
        UpdateAutoRefresh();
        NotifyStateChanged();
    }

    public Task<Res> RefreshAsync()
    {
        return RunAsync(() => Task.FromResult(LoadDashboardData()));
    }

    public Task<Res> ForceGarbageCollectionAsync(int generation)
    {
        return RunAsync(() =>
        {
            var result = memoryDiagnosticsFacade.ForceGarbageCollection(generation);
            if (result.IsFailed(out var error))
            {
                return Task.FromResult<Res>(Res.Fail(error.Message ?? "Failed to execute GC."));
            }

            return Task.FromResult(LoadDashboardData());
        });
    }

    public Task<Res<string>> CreateGcDumpAsync()
    {
        return RunAsync(() => memoryDiagnosticsFacade.TriggerGcDumpAsync());
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }

    private Res LoadDashboardData()
    {
        var snapshotResult = memoryDiagnosticsFacade.GetCurrentSnapshot();
        if (snapshotResult.IsFailed(out var snapshotError, out var snapshot))
        {
            return Res.Fail(snapshotError.Message ?? "Failed to capture memory snapshot.");
        }

        var gcResult = memoryDiagnosticsFacade.GetGcDetails();
        if (gcResult.IsFailed(out var gcError, out var gcDetails))
        {
            return Res.Fail(gcError.Message ?? "Failed to capture GC details.");
        }

        var trendResult = runtimeMetricsFacade.GetTrend();
        if (trendResult.IsFailed(out var trendError, out var trend))
        {
            return Res.Fail(trendError.Message ?? "Failed to load runtime metrics trend.");
        }

        CurrentSnapshot = snapshot;
        CurrentGcDetails = gcDetails;
        TrendData = trend;
        return Res.Ok();
    }

    private async Task<Res> RunAsync(Func<Task<Res>> action)
    {
        if (IsLoading)
        {
            return Res.Fail("The memory analysis page is already busy.");
        }

        IsLoading = true;
        NotifyStateChanged();

        try
        {
            return await action();
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    private async Task<Res<T>> RunAsync<T>(Func<Task<Res<T>>> action)
    {
        if (IsLoading)
        {
            return Res.Fail("The memory analysis page is already busy.");
        }

        IsLoading = true;
        NotifyStateChanged();

        try
        {
            return await action();
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    private void UpdateAutoRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;

        if (!AutoRefresh || _option.AutoRefreshIntervalMs <= 0)
        {
            return;
        }

        _refreshTimer = new Timer(
            async _ => await RefreshAsync(),
            null,
            _option.AutoRefreshIntervalMs,
            _option.AutoRefreshIntervalMs);
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
