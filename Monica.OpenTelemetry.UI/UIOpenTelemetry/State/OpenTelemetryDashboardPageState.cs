using Monica.Core.Results;
using Monica.OpenTelemetry.InProcessCollector.Facades;
using Monica.OpenTelemetry.InProcessCollector.Models;

namespace Monica.OpenTelemetry.UI.UIOpenTelemetry.State;

/// <summary>
/// Owns one metrics dashboard's snapshot, filters, refresh loop, and async lifetime.
/// </summary>
public sealed class OpenTelemetryDashboardPageState(
    OpenTelemetryFacade facade,
    string snapshotLoadFailedMessage) : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly SemaphoreSlim _timerGate = new(1, 1);
    private PeriodicTimer? _autoRefreshTimer;
    private Task? _autoRefreshTask;
    private bool _disposed;

    /// <summary>
    /// Raised whenever the dashboard should re-render.
    /// </summary>
    public event Func<Task>? StateChanged;

    /// <summary>
    /// Gets the latest loaded in-process metric snapshot.
    /// </summary>
    public OpenTelemetrySnapshot? Snapshot { get; private set; }

    /// <summary>
    /// Gets whether a snapshot request is in progress.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Gets the latest dashboard load error, if any.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Gets or sets the meter filter. Empty means all meters.
    /// </summary>
    public string MeterPrefixFilter { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the case-insensitive instrument name substring filter.
    /// </summary>
    public string InstrumentNameFilter { get; set; } = string.Empty;

    /// <summary>
    /// Gets the auto-refresh interval. Null disables automatic refresh.
    /// </summary>
    public TimeSpan? AutoRefreshInterval { get; private set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Loads the initial dashboard snapshot and starts the auto-refresh loop when configured.
    /// </summary>
    public async Task<Res> LoadAsync()
    {
        ThrowIfDisposed();
        var result = await RefreshAsync();
        await RestartAutoRefreshLoopAsync();
        return result;
    }

    /// <summary>
    /// Refreshes the dashboard snapshot while joining the component-owned lifetime.
    /// </summary>
    public async Task<Res> RefreshAsync()
    {
        var cancellationToken = _lifetimeCancellation.Token;
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            IsLoading = true;
            Error = null;
            await NotifyStateChangedAsync();

            var result = await facade.GetSnapshotAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsFailed(out var error, out var snapshot) || snapshot is null)
            {
                Error = error?.Message ?? snapshotLoadFailedMessage;
                return Res.Fail(Error);
            }

            Snapshot = snapshot;
            return Res.Ok();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Res.Ok();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return Res.Fail(Error);
        }
        finally
        {
            if (!_disposed)
            {
                IsLoading = false;
                await NotifyStateChangedAsync();
            }

            _refreshGate.Release();
        }
    }

    /// <summary>
    /// Updates the auto-refresh interval and restarts the owned loop.
    /// </summary>
    public async Task SetAutoRefreshIntervalAsync(TimeSpan? interval)
    {
        ThrowIfDisposed();
        AutoRefreshInterval = interval;
        await RestartAutoRefreshLoopAsync();
        await NotifyStateChangedAsync();
    }

    /// <summary>
    /// Gets the meter names present in the current snapshot.
    /// </summary>
    public IReadOnlyList<string> GetMeterNames()
    {
        return Snapshot?.Instruments
                   .Select(static instrument => instrument.MeterName)
                   .Distinct(StringComparer.Ordinal)
                   .Order(StringComparer.Ordinal)
                   .ToList()
               ?? [];
    }

    /// <summary>
    /// Gets filtered instruments based on the current meter and name filters.
    /// </summary>
    public IReadOnlyList<InstrumentSnapshot> FilteredInstruments()
    {
        if (Snapshot is null)
        {
            return [];
        }

        var query = Snapshot.Instruments.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(MeterPrefixFilter))
        {
            query = query.Where(instrument => string.Equals(
                instrument.MeterName,
                MeterPrefixFilter,
                StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(InstrumentNameFilter))
        {
            query = query.Where(instrument => instrument.Name.Contains(
                InstrumentNameFilter,
                StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }

    /// <summary>
    /// Groups filtered instruments by meter name.
    /// </summary>
    public IReadOnlyList<IGrouping<string, InstrumentSnapshot>> FilteredInstrumentGroups()
    {
        return FilteredInstruments()
            .GroupBy(static instrument => instrument.MeterName)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Serializes a compact current-state JSON view for clipboard export.
    /// </summary>
    public string ExportJson() => Snapshot is null
        ? string.Empty
        : OpenTelemetryJsonSummaryExporter.Export(Snapshot);

    /// <summary>
    /// Converts the current snapshot to a Prometheus-compatible text view.
    /// </summary>
    public string ExportPrometheusText() => Snapshot is null
        ? string.Empty
        : PrometheusTextExporter.Export(Snapshot);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StateChanged = null;
        await _lifetimeCancellation.CancelAsync();
        await StopAutoRefreshLoopAsync();

        await _refreshGate.WaitAsync();
        _refreshGate.Release();
        _refreshGate.Dispose();
        _timerGate.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private async Task RestartAutoRefreshLoopAsync()
    {
        await _timerGate.WaitAsync(_lifetimeCancellation.Token);
        try
        {
            await StopAutoRefreshLoopCoreAsync();
            if (_disposed || AutoRefreshInterval is null || AutoRefreshInterval <= TimeSpan.Zero)
            {
                return;
            }

            _autoRefreshTimer = new PeriodicTimer(AutoRefreshInterval.Value);
            _autoRefreshTask = RunAutoRefreshLoopAsync(_autoRefreshTimer, _lifetimeCancellation.Token);
        }
        finally
        {
            _timerGate.Release();
        }
    }

    private async Task StopAutoRefreshLoopAsync()
    {
        await _timerGate.WaitAsync();
        try
        {
            await StopAutoRefreshLoopCoreAsync();
        }
        finally
        {
            _timerGate.Release();
        }
    }

    private async Task StopAutoRefreshLoopCoreAsync()
    {
        var timer = _autoRefreshTimer;
        var task = _autoRefreshTask;
        _autoRefreshTimer = null;
        _autoRefreshTask = null;
        timer?.Dispose();

        if (task is not null)
        {
            await task;
        }
    }

    private async Task RunAutoRefreshLoopAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshAsync();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when the page changes interval or is disposed.
        }
    }

    private async Task NotifyStateChangedAsync()
    {
        var handlers = StateChanged?.GetInvocationList().Cast<Func<Task>>().ToArray() ?? [];
        foreach (var handler in handlers)
        {
            await handler();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
