using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.Profiling.RuntimeMetrics.Facades;
using Monica.Core.Results;
using Monica.Profiling.RuntimeMetrics.Models;

namespace Monica.Profiling.UIProfiling.State;

public sealed class ProfilingMonitorPageState(
    RuntimeMetricsFacade runtimeMetricsFacade,
    IOptions<ModuleProfilingUIOption> options)
    : IDisposable
{
    private readonly ModuleProfilingUIOption _option = options.Value;
    private Timer? _refreshTimer;

    public event Action? StateChanged;

    public RuntimeMetricsPoint? CurrentDataPoint { get; private set; }

    public RuntimeMetricsPoint? PreviousDataPoint { get; private set; }

    public RuntimeMetricsTrend? TrendData { get; private set; }

    public bool IsLoading { get; private set; }

    public bool AutoRefresh { get; private set; } = true;

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

    public void Dispose()
    {
        _refreshTimer?.Dispose();
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
