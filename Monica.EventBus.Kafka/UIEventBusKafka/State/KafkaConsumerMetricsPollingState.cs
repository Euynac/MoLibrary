using Microsoft.Extensions.Options;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.EventBus.Kafka.Facades;
using Monica.EventBus.Kafka.Models;
using Monica.Modules;

namespace Monica.EventBus.Kafka.UIEventBusKafka.State;

/// <summary>
/// Owns live sampling for one consumer-group member and partition detail view.
/// </summary>
public sealed class KafkaConsumerMetricsPollingState(
    KafkaConsoleFacade facade,
    IOptions<ModuleEventBusKafkaUIOption> uiOptions) : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private string? _clusterId;
    private string? _groupId;
    private string? _topicName;
    private TimeSpan _refreshInterval = NormalizeInterval(uiOptions.Value.PerformanceRefreshInterval);
    private bool _isEnabled = uiOptions.Value.EnablePerformanceAutoRefresh;
    private bool _disposed;

    /// <summary>
    /// Gets the latest captured member and partition metrics.
    /// </summary>
    public KafkaConsumerMetricsSnapshot? Snapshot { get; private set; }

    /// <summary>
    /// Gets whether automatic refresh is enabled.
    /// </summary>
    public bool IsAutomaticRefreshEnabled => _isEnabled;

    /// <summary>
    /// Gets the current interval in whole seconds.
    /// </summary>
    public int RefreshIntervalSeconds => (int)_refreshInterval.TotalSeconds;

    /// <summary>
    /// Gets whether a Kafka metrics query is in progress.
    /// </summary>
    public bool IsSampling { get; private set; }

    /// <summary>
    /// Gets the latest sampling error or provider diagnostic.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Raised whenever the capture state changes.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// Activates sampling for one consumer group and an optional topic filter.
    /// </summary>
    /// <param name="clusterId">Target cluster identifier.</param>
    /// <param name="groupId">Consumer group identifier.</param>
    /// <param name="topicName">Optional topic filter.</param>
    /// <param name="cancellationToken">Cancellation token for the initial capture.</param>
    public async Task ActivateAsync(
        string clusterId,
        string groupId,
        string? topicName,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        await StopLoopAsync();
        _clusterId = clusterId.Trim();
        _groupId = groupId.Trim();
        _topicName = string.IsNullOrWhiteSpace(topicName) ? null : topicName.Trim();
        Snapshot = null;
        LastError = null;
        NotifyStateChanged();

        await CaptureOnceAsync(cancellationToken, waitForSlot: true);
        if (_isEnabled)
        {
            StartLoop();
        }
    }

    /// <summary>
    /// Captures one sample immediately.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when a snapshot was captured.</returns>
    public Task<bool> CaptureNowAsync(CancellationToken cancellationToken = default)
    {
        return CaptureOnceAsync(cancellationToken, waitForSlot: true);
    }

    /// <summary>
    /// Enables or disables automatic refresh.
    /// </summary>
    /// <param name="enabled">Whether automatic refresh should run.</param>
    /// <param name="cancellationToken">Cancellation token for an immediate enabling capture.</param>
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
            StartLoop();
        }
    }

    /// <summary>
    /// Changes the automatic refresh interval.
    /// </summary>
    /// <param name="seconds">Requested interval in seconds.</param>
    public async Task SetRefreshIntervalAsync(int seconds)
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
            StartLoop();
        }
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
        await _captureGate.WaitAsync();
        _captureGate.Release();
        _captureGate.Dispose();
        _lifetimeCts.Dispose();
    }

    private async Task<bool> CaptureOnceAsync(CancellationToken cancellationToken, bool waitForSlot)
    {
        ThrowIfDisposed();
        if (_clusterId is null || _groupId is null)
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
            var result = await facade.CaptureConsumerMetricsAsync(
                _clusterId,
                _topicName,
                _groupId,
                cancellationToken);
            if (result.IsFailed(out var error))
            {
                LastError = error.Message;
                return false;
            }

            Snapshot = result.Data!;
            LastError = Snapshot.Message;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastError = ex.GetMessageRecursively();
            return false;
        }
        finally
        {
            IsSampling = false;
            _captureGate.Release();
            NotifyStateChanged();
        }
    }

    private void StartLoop()
    {
        if (_disposed || !_isEnabled || _clusterId is null || _loopTask is { IsCompleted: false })
        {
            return;
        }

        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        _timer = new PeriodicTimer(_refreshInterval);
        _loopTask = RunLoopAsync(_timer, _loopCts.Token);
    }

    private async Task RunLoopAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await CaptureOnceAsync(cancellationToken, waitForSlot: false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Closing the dialog and changing sampling controls are normal cancellation paths.
        }
        catch (ObjectDisposedException) when (_disposed)
        {
            // Disposal can race an already queued timer tick.
        }
    }

    private async Task StopLoopAsync()
    {
        var loopCts = _loopCts;
        var loopTask = _loopTask;
        _loopCts = null;
        _loopTask = null;
        _timer?.Dispose();
        _timer = null;
        if (loopCts is null)
        {
            return;
        }

        loopCts.Cancel();
        if (loopTask is not null)
        {
            await loopTask;
        }

        loopCts.Dispose();
    }

    private static TimeSpan NormalizeInterval(TimeSpan interval)
    {
        var seconds = Math.Clamp(
            (int)Math.Round(interval.TotalSeconds),
            KafkaPerformancePollingState.MinimumRefreshIntervalSeconds,
            KafkaPerformancePollingState.MaximumRefreshIntervalSeconds);
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
