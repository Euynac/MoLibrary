using Microsoft.Extensions.Options;
using Monica.Core.Results;
using Monica.Modules;
using Monica.Profiling.ExecutionTiming.Facades;
using Monica.Profiling.ExecutionTiming.Models;

namespace Monica.Profiling.UIExecutionTiming.State;

public sealed class ExecutionTimingPageState(
    ExecutionTimingFacade executionTimingFacade,
    IOptions<ModuleExecutionTimingUIOption> options)
    : IDisposable
{
    private readonly ModuleExecutionTimingUIOption _option = options.Value;
    private Timer? _refreshTimer;

    public event Action? StateChanged;

    public IReadOnlyList<ExecutionTimingStatistics> Statistics { get; private set; } = [];

    public IReadOnlyList<RunningExecutionTimingInfo> RunningOperations { get; private set; } = [];

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
        return RunAsync(() => Task.FromResult(LoadData()));
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }

    private Res LoadData()
    {
        var statisticsResult = executionTimingFacade.GetStatistics();
        if (statisticsResult.IsFailed(out var statisticsError, out var statistics))
        {
            return Res.Fail(statisticsError.Message ?? "Failed to load execution timing statistics.");
        }

        var runningResult = executionTimingFacade.GetRunningOperations();
        if (runningResult.IsFailed(out var runningError, out var runningOperations))
        {
            return Res.Fail(runningError.Message ?? "Failed to load running execution timing operations.");
        }

        Statistics = statistics;
        RunningOperations = runningOperations;
        return Res.Ok();
    }

    private async Task<Res> RunAsync(Func<Task<Res>> action)
    {
        if (IsLoading)
        {
            return Res.Fail("The execution timing page is already busy.");
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
