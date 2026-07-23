using Microsoft.Extensions.Options;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.EventBus.Kafka.Facades;
using Monica.EventBus.Kafka.Models;
using Monica.Modules;

namespace Monica.EventBus.Kafka.UIEventBusKafka.State;

/// <summary>
/// Owns the page-scoped live Kafka performance sampler.
/// </summary>
/// <remarks>
/// The sampler deliberately lives outside the Razor page. It serializes manual and automatic
/// captures, cancels its <see cref="PeriodicTimer"/> when the selected cluster changes, and keeps
/// only the latest snapshot for the performance and dashboard tabs.
/// </remarks>
public sealed class KafkaPerformancePollingState(
    KafkaConsoleFacade facade,
    IOptions<ModuleEventBusKafkaUIOption> uiOptions) : IAsyncDisposable
{
    /// <summary>
    /// Minimum interval accepted by the live sampler.
    /// </summary>
    public const int MinimumRefreshIntervalSeconds = 1;

    /// <summary>
    /// Maximum interval accepted by the live sampler.
    /// </summary>
    public const int MaximumRefreshIntervalSeconds = 3600;

    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private readonly object _snapshotSync = new();

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private string? _clusterId;
    private bool _isEnabled = uiOptions.Value.EnablePerformanceAutoRefresh;
    private TimeSpan _refreshInterval = NormalizeInterval(uiOptions.Value.PerformanceRefreshInterval);
    private KafkaPerformanceSnapshot? _latestSnapshot;
    private DateTimeOffset? _lastCapturedAt;
    private bool _disposed;

    /// <summary>
    /// Gets the most recent live snapshot, when one exists.
    /// </summary>
    public KafkaPerformanceSnapshot? LatestSnapshot
    {
        get
        {
            lock (_snapshotSync)
            {
                return _latestSnapshot;
            }
        }
    }

    /// <summary>
    /// Gets whether automatic sampling is enabled.
    /// </summary>
    public bool IsAutomaticRefreshEnabled => _isEnabled;

    /// <summary>
    /// Gets the current automatic sampling interval.
    /// </summary>
    public TimeSpan RefreshInterval => _refreshInterval;

    /// <summary>
    /// Gets the current interval in whole seconds for numeric UI controls.
    /// </summary>
    public int RefreshIntervalSeconds => (int)_refreshInterval.TotalSeconds;

    /// <summary>
    /// Gets whether a capture is currently querying Kafka.
    /// </summary>
    public bool IsSampling { get; private set; }

    /// <summary>
    /// Gets the timestamp of the latest completed capture.
    /// </summary>
    public DateTimeOffset? LastCapturedAt
    {
        get
        {
            lock (_snapshotSync)
            {
                return _lastCapturedAt;
            }
        }
    }

    /// <summary>
    /// Gets the latest sampling diagnostic, if the broker returned one.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Raised when the sampler state or latest snapshot changes.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// Replaces the latest snapshot loaded from the repository for a selected cluster.
    /// </summary>
    /// <param name="snapshot">Latest snapshot, or <see langword="null"/> when none has been captured.</param>
    public void SetSnapshot(KafkaPerformanceSnapshot? snapshot)
    {
        ReplaceLatestSnapshot(snapshot);
        LastError = null;
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears the latest snapshot while switching away from a cluster.
    /// </summary>
    public void ClearSnapshot()
    {
        SetSnapshot(null);
    }

    /// <summary>
    /// Activates live sampling for a reachable cluster.
    /// </summary>
    /// <param name="clusterId">Cluster to sample.</param>
    /// <param name="canSample">Whether direct Kafka admin access is currently available.</param>
    /// <param name="cancellationToken">Operation cancellation token.</param>
    public async Task ActivateAsync(
        string? clusterId,
        bool canSample,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var normalizedClusterId = canSample && !string.IsNullOrWhiteSpace(clusterId)
            ? clusterId.Trim()
            : null;
        var sameCluster = string.Equals(_clusterId, normalizedClusterId, StringComparison.Ordinal);
        if (sameCluster && (!_isEnabled || IsRunning))
        {
            return;
        }

        await StopLoopAsync();
        _clusterId = normalizedClusterId;
        LastError = null;
        NotifyStateChanged();

        if (_clusterId is null || !_isEnabled)
        {
            return;
        }

        // Capture immediately to establish a baseline; the next interval then yields a real
        // write/consume rate instead of waiting for a manual button click.
        await CaptureOnceAsync(cancellationToken, waitForSlot: true);
        StartLoop(_clusterId);
    }

    /// <summary>
    /// Enables or disables automatic sampling for the active cluster.
    /// </summary>
    /// <param name="enabled">Whether periodic captures should run.</param>
    /// <param name="cancellationToken">Operation cancellation token.</param>
    public async Task SetAutomaticRefreshAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_isEnabled == enabled)
        {
            return;
        }

        _isEnabled = enabled;
        await StopLoopAsync();
        NotifyStateChanged();

        if (_isEnabled && _clusterId is not null)
        {
            await CaptureOnceAsync(cancellationToken, waitForSlot: true);
            StartLoop(_clusterId);
        }
    }

    /// <summary>
    /// Changes the automatic sampling interval.
    /// </summary>
    /// <param name="seconds">Requested interval in seconds.</param>
    /// <param name="cancellationToken">Operation cancellation token.</param>
    public async Task SetRefreshIntervalAsync(int seconds, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var normalized = NormalizeInterval(TimeSpan.FromSeconds(seconds));
        if (normalized == _refreshInterval)
        {
            return;
        }

        _refreshInterval = normalized;
        await StopLoopAsync();
        NotifyStateChanged();

        if (_isEnabled && _clusterId is not null)
        {
            StartLoop(_clusterId);
        }
    }

    /// <summary>
    /// Captures one snapshot immediately, regardless of the automatic-refresh setting.
    /// </summary>
    /// <param name="cancellationToken">Operation cancellation token.</param>
    /// <returns><see langword="true"/> when a snapshot was returned by the facade.</returns>
    public Task<bool> CaptureNowAsync(CancellationToken cancellationToken = default)
    {
        return CaptureOnceAsync(cancellationToken, waitForSlot: true);
    }

    /// <summary>
    /// Stops the periodic loop while retaining the current snapshot and settings.
    /// </summary>
    public Task DeactivateAsync()
    {
        return StopLoopAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopLoopAsync();
        _disposed = true;
        _lifetimeCts.Cancel();
        // A manual capture can outlive the periodic loop. Wait for its gate before disposing the
        // semaphore so the finally block in CaptureOnceAsync can always release it safely.
        await _captureGate.WaitAsync();
        _captureGate.Release();
        _captureGate.Dispose();
        _lifetimeCts.Dispose();
    }

    private bool IsRunning => _loopTask is { IsCompleted: false };

    private async Task<bool> CaptureOnceAsync(CancellationToken cancellationToken, bool waitForSlot)
    {
        ThrowIfDisposed();
        var clusterId = _clusterId;
        if (clusterId is null)
        {
            return false;
        }

        if (waitForSlot)
        {
            await _captureGate.WaitAsync(cancellationToken);
        }
        else if (!await _captureGate.WaitAsync(0, cancellationToken))
        {
            return false;
        }

        if (_disposed)
        {
            _captureGate.Release();
            return false;
        }

        IsSampling = true;
        NotifyStateChanged();
        try
        {
            var result = await facade.CapturePerformanceAsync(clusterId, cancellationToken);
            if (result.IsFailed(out var error))
            {
                LastError = error.Message;
                NotifyStateChanged();
                return false;
            }

            var snapshot = result.Data!;
            ReplaceLatestSnapshot(snapshot);
            LastError = snapshot.Message;
            NotifyStateChanged();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastError = ex.GetMessageRecursively();
            NotifyStateChanged();
            return false;
        }
        finally
        {
            IsSampling = false;
            _captureGate.Release();
            NotifyStateChanged();
        }
    }

    private void ReplaceLatestSnapshot(KafkaPerformanceSnapshot? snapshot)
    {
        lock (_snapshotSync)
        {
            _latestSnapshot = snapshot;
            _lastCapturedAt = snapshot?.CapturedAt;
        }
    }

    private void StartLoop(string clusterId)
    {
        if (_disposed || !_isEnabled || IsRunning)
        {
            return;
        }

        var loopCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        var timer = new PeriodicTimer(_refreshInterval);
        _loopCts = loopCts;
        _timer = timer;
        _loopTask = RunLoopAsync(clusterId, timer, loopCts.Token);
    }

    private async Task RunLoopAsync(string clusterId, PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!string.Equals(_clusterId, clusterId, StringComparison.Ordinal))
                {
                    return;
                }

                await CaptureOnceAsync(cancellationToken, waitForSlot: false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Navigation, interval changes, and disposal are expected cancellation paths.
        }
        catch (ObjectDisposedException) when (_disposed)
        {
            // Disposal can race a timer tick that was already queued.
        }
    }

    private async Task StopLoopAsync()
    {
        var loopCts = _loopCts;
        var loopTask = _loopTask;
        var timer = _timer;
        _loopCts = null;
        _loopTask = null;
        _timer = null;

        timer?.Dispose();
        if (loopCts is null)
        {
            return;
        }

        loopCts.Cancel();
        if (loopTask is not null)
        {
            try
            {
                await loopTask;
            }
            catch (OperationCanceledException)
            {
                // The loop owns cancellation as part of its normal shutdown protocol.
            }
        }

        loopCts.Dispose();
    }

    private static TimeSpan NormalizeInterval(TimeSpan interval)
    {
        var seconds = Math.Clamp(
            (int)Math.Round(interval.TotalSeconds),
            MinimumRefreshIntervalSeconds,
            MaximumRefreshIntervalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
