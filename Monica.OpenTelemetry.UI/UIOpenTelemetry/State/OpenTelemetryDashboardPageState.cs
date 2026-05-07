using Monica.Core.Results;
using Monica.OpenTelemetry.InProcessCollector.Facades;
using Monica.OpenTelemetry.InProcessCollector.Models;

namespace Monica.OpenTelemetry.UI.UIOpenTelemetry.State;

/// <summary>
/// Holds mutable state and snapshot projection logic for the OpenTelemetry metrics dashboard.
/// </summary>
public sealed class OpenTelemetryDashboardPageState(OpenTelemetryFacade facade) : IDisposable
{
    private PeriodicTimer? _autoRefreshTimer;
    private CancellationTokenSource? _autoRefreshCts;
    private Task? _autoRefreshTask;

    /// <summary>
    /// Raised whenever the dashboard should re-render.
    /// </summary>
    public event Action? StateChanged;

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
    /// Gets or sets the auto-refresh interval. Null disables automatic refresh.
    /// </summary>
    public TimeSpan? AutoRefreshInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Loads the initial dashboard snapshot and starts the auto-refresh loop when configured.
    /// </summary>
    public async Task<Res> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await RefreshAsync(cancellationToken);
        RestartAutoRefreshLoop();
        return result;
    }

    /// <summary>
    /// Refreshes the dashboard snapshot.
    /// </summary>
    public async Task<Res> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return Res.Ok();
        }

        IsLoading = true;
        Error = null;
        NotifyStateChanged();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await facade.GetSnapshotAsync();
            if (result.IsFailed(out var error, out var snapshot) || snapshot is null)
            {
                Error = error?.Message;
                return Res.Fail(Error ?? "Failed to load OpenTelemetry metrics snapshot.");
            }

            Snapshot = snapshot;
            return Res.Ok();
        }
        catch (OperationCanceledException)
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
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Updates the auto-refresh interval and restarts the timer loop.
    /// </summary>
    public void SetAutoRefreshInterval(TimeSpan? interval)
    {
        AutoRefreshInterval = interval;
        RestartAutoRefreshLoop();
        NotifyStateChanged();
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
    public string ExportJson()
    {
        return Snapshot is null
            ? string.Empty
            : OpenTelemetryJsonSummaryExporter.Export(Snapshot);
    }

    /// <summary>
    /// Converts the current snapshot to a simple Prometheus-compatible text view for clipboard export.
    /// </summary>
    public string ExportPrometheusText()
    {
        if (Snapshot is null)
        {
            return string.Empty;
        }

        return PrometheusTextExporter.Export(Snapshot);
    }

    public void Dispose()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts?.Dispose();
        _autoRefreshTimer?.Dispose();
    }

    private void RestartAutoRefreshLoop()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts?.Dispose();
        _autoRefreshTimer?.Dispose();
        _autoRefreshCts = null;
        _autoRefreshTimer = null;
        _autoRefreshTask = null;

        if (AutoRefreshInterval is null || AutoRefreshInterval <= TimeSpan.Zero)
        {
            return;
        }

        _autoRefreshCts = new CancellationTokenSource();
        _autoRefreshTimer = new PeriodicTimer(AutoRefreshInterval.Value);
        _autoRefreshTask = RunAutoRefreshLoopAsync(_autoRefreshCts.Token);
    }

    private async Task RunAutoRefreshLoopAsync(CancellationToken cancellationToken)
    {
        if (_autoRefreshTimer is null)
        {
            return;
        }

        try
        {
            while (await _autoRefreshTimer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
