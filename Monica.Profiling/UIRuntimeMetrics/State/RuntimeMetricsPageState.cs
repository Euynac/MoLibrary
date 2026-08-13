using Microsoft.Extensions.Options;
using Monica.Core.Results;
using Monica.Modules;
using Monica.Profiling.RuntimeMetrics.Facades;
using Monica.Profiling.RuntimeMetrics.Models;

namespace Monica.Profiling.UIRuntimeMetrics.State;

public sealed class RuntimeMetricsPageState(
    RuntimeMetricsFacade runtimeMetricsFacade,
    IOptions<ModuleRuntimeMetricsUIOption> options)
    : IAsyncDisposable
{
    private readonly ModuleRuntimeMetricsUIOption _option = options.Value;
    private CancellationTokenSource? _refreshCancellation;
    private Task _refreshLoop = Task.CompletedTask;
    private bool _disposed;

    public event Action? StateChanged;

    public RuntimeMetricsPoint? CurrentDataPoint { get; private set; }

    public RuntimeMetricsPoint? PreviousDataPoint { get; private set; }

    public RuntimeMetricsTrend? TrendData { get; private set; }

    public bool IsLoading { get; private set; }

    public bool AutoRefresh { get; private set; } = true;

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
        if (IsLoading)
        {
            return Task.FromResult<Res>(Res.Ok());
        }

        IsLoading = true;
        NotifyStateChanged();

        try
        {
            var result = runtimeMetricsFacade.GetTrend();
            if (result.IsFailed(out var error, out var trend))
            {
                return Task.FromResult<Res>(Res.Fail(error.Message ?? "Failed to load runtime metrics."));
            }

            TrendData = trend;
            PreviousDataPoint = CurrentDataPoint;
            CurrentDataPoint = trend.DataPoints.LastOrDefault();
            return Task.FromResult<Res>(Res.Ok());
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
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
