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
    : IAsyncDisposable
{
    private readonly ModuleMemoryAnalysisUIOption _option = options.Value;
    private CancellationTokenSource? _refreshCancellation;
    private Task _refreshLoop = Task.CompletedTask;
    private bool _disposed;

    public event Action? StateChanged;

    public MemorySnapshot? CurrentSnapshot { get; private set; }

    public GcDetails? CurrentGcDetails { get; private set; }

    public RuntimeMetricsTrend? TrendData { get; private set; }

    public bool IsLoading { get; private set; }

    public bool AutoRefresh { get; private set; } = true;

    public int ActiveTabIndex { get; set; }

    /// <summary>
    /// Gets the frozen host options used by diagnostics controls on this page.
    /// </summary>
    public ModuleMemoryAnalysisUIOption Options => _option;

    public bool IsTypeAllocationTabEnabled => _option.EnableTypeAllocationTab;

    public async Task<Res> InitializeAsync()
    {
        var result = await RefreshAsync();
        await UpdateAutoRefreshAsync();
        return result;
    }

    public async Task SetAutoRefreshAsync(bool value)
    {
        if (AutoRefresh == value)
        {
            return;
        }

        AutoRefresh = value;
        await UpdateAutoRefreshAsync();
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopRefreshLoopAsync();
        StateChanged = null;
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

    private async Task UpdateAutoRefreshAsync()
    {
        await StopRefreshLoopAsync();

        if (!AutoRefresh || _option.AutoRefreshIntervalMs <= 0)
        {
            return;
        }

        _refreshCancellation = new CancellationTokenSource();
        _refreshLoop = RunRefreshLoopAsync(_refreshCancellation.Token);
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_option.AutoRefreshIntervalMs));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshAsync();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task StopRefreshLoopAsync()
    {
        var cancellation = _refreshCancellation;
        _refreshCancellation = null;
        cancellation?.Cancel();

        try
        {
            await _refreshLoop;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cancellation?.Dispose();
            _refreshLoop = Task.CompletedTask;
        }
    }

    private void NotifyStateChanged()
    {
        if (!_disposed)
        {
            StateChanged?.Invoke();
        }
    }
}
