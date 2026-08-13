using Microsoft.Extensions.Options;
using Monica.Core.Results;
using Monica.Modules;
using Monica.Profiling.ExecutionTiming.Facades;
using Monica.Profiling.ExecutionTiming.Models;

namespace Monica.Profiling.UIExecutionTiming.State;

public sealed class ExecutionTimingPageState(
    ExecutionTimingFacade executionTimingFacade,
    IOptions<ModuleExecutionTimingUIOption> options)
    : IAsyncDisposable
{
    private readonly ModuleExecutionTimingUIOption _option = options.Value;
    private CancellationTokenSource? _refreshCancellation;
    private Task _refreshLoop = Task.CompletedTask;
    private bool _disposed;

    public event Action? StateChanged;

    public IReadOnlyList<ExecutionTimingStatistics> Statistics { get; private set; } = [];

    public IReadOnlyList<RunningExecutionTimingInfo> RunningOperations { get; private set; } = [];

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
        return RunAsync(() => Task.FromResult(LoadData()));
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
