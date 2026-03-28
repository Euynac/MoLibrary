using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.Framework.UI.UILogging.Models;
using Monica.Tool.Extensions;
using Monica.Core.Results;

namespace Monica.Framework.UI.UILogging.Services;

/// <summary>
/// Log page core service, responsible for maintaining temporary log pool and real-time subscription
/// </summary>
public sealed class LoggingService(
    ScreenLogBuffer buffer,
    LogTailService logTailService,
    LogFileQueryService logFileQueryService,
    IOptions<ModuleLoggingUIOption> uiOptions,
    ILogger<LoggingService> logger)
{
    private readonly object _subscriberLock = new();
    private readonly List<Channel<ScreenLogSnapshot>> _subscribers = new();

    private CancellationTokenSource? _tailCts;
    private Task? _tailTask;
    private volatile bool _isRunning;

    public ModuleLoggingUIOption UIOption => uiOptions.Value;

    public string CurrentLogFilePath => logTailService.LogFilePath;

    public string LogDirectory => logFileQueryService.LogDirectory;

    public bool IsRunning => _isRunning;

    public long TotalFileLineCount => logTailService.TotalFileLineCount;

    /// <summary>
    /// Does the current log file exist?
    /// </summary>
    public bool LogFileExists => logTailService.LogFileExists;

    /// <summary>
    /// Initialize log buffer pool
    /// </summary>
    public async Task<Res<ScreenLogSnapshot>> InitializeAsync(int requestedLines, CancellationToken cancellationToken = default)
    {
        var maxLines = buffer.MaxDisplayLines;
        var lineCount = Math.Clamp(requestedLines, 1, maxLines);
        var result = await logTailService.ReadLatestLinesAsync(lineCount, cancellationToken);
        if (result.IsFailed(out var error, out var readResult))
        {
            return error;
        }

        var snapshot = buffer.Reset(readResult.Lines, readResult.StartLineNumber);
        await PublishSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return snapshot;
    }

    /// <summary>
    /// Start real-time log monitoring
    /// </summary>
    public Task<Res> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            return Task.FromResult(Res.Ok("日志监听已在运行"));
        }

        // Make sure to release the old CancellationTokenSource
        _tailCts.SafeCancelAndDispose();

        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _tailCts = linked;
        _tailTask = Task.Run(() => TailLoopAsync(linked.Token));
        _isRunning = true;
        logger.LogInformation("日志监听任务已启动");
        return Task.FromResult(Res.Ok("日志监听已启动"));
    }

    /// <summary>
    /// Pause log monitoring
    /// </summary>
    public async Task<Res> PauseAsync()
    {
        if (!_isRunning)
        {
            return Res.Ok("日志监听已暂停");
        }

        try
        {
            if (_tailCts != null)
            {
                try { await _tailCts.CancelAsync(); }
                catch (ObjectDisposedException) { }
            }
            if (_tailTask is { } task)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // ignore
                }
            }
        }
        finally
        {
            _tailTask = null;
            _tailCts.SafeCancelAndDispose();
            _tailCts = null;
            _isRunning = false;
            logger.LogInformation("日志监听任务已停止");
        }

        return Res.Ok("日志监听已暂停");
    }

    /// <summary>
    /// Update filter
    /// </summary>
    public async Task<Res<ScreenLogSnapshot>> UpdateFilterAsync(string? keyword, bool onlyCapture)
    {
        var filter = LogFilterState.Create(keyword, onlyCapture);
        var snapshot = buffer.ApplyFilter(filter);
        await PublishSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
        return snapshot;
    }

    /// <summary>
    /// Get current snapshot
    /// </summary>
    public ScreenLogSnapshot GetSnapshot()
    {
        return buffer.Snapshot();
    }

    /// <summary>
    /// Load more history logs (before current buffer)
    /// </summary>
    /// <param name="lineCount">Number of lines to load, default 50</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    /// <returns>Number of rows loaded</returns>
    public async Task<Res<int>> LoadMoreLinesAsync(int lineCount = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = buffer.Snapshot();
            if (snapshot.Lines.Count == 0)
            {
                return 0;
            }

            // Get the earliest line number of the current buffer
            var firstLine = snapshot.Lines.FirstOrDefault();
            if (firstLine?.AbsoluteLineNumber == null || firstLine.AbsoluteLineNumber <= 1)
            {
                // The beginning of the file has been reached
                return 0;
            }

            var beforeLineNumber = firstLine.AbsoluteLineNumber.Value;
            var result = await logTailService.ReadLinesBeforeAsync(beforeLineNumber, lineCount, cancellationToken);

            if (result.IsFailed(out var error, out var readResult))
            {
                return error;
            }

            if (readResult.Lines.Count == 0)
            {
                return 0;
            }

            // prepend to buffer
            var newSnapshot = buffer.Prepend(readResult.Lines, readResult.StartLineNumber);
            await PublishSnapshotAsync(newSnapshot, cancellationToken).ConfigureAwait(false);

            return readResult.Lines.Count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "加载更多日志失败");
            return $"加载更多日志失败: {ex.Message}";
        }
    }

    /// <summary>
    /// Export the current temporary log pool
    /// </summary>
    public Task<Res<LogExportResult>> ExportBufferAsync()
    {
        var content = buffer.ExportCurrent();
        var bytes = Encoding.UTF8.GetBytes(content);
        var fileName = $"temp-logs-{DateTimeOffset.Now:yyyyMMddHHmmss}.log";
        var result = new LogExportResult(fileName, "text/plain", bytes);
        return Task.FromResult((Res<LogExportResult>)result);
    }

    /// <summary>
    /// List log directory files
    /// </summary>
    public Task<Res<IReadOnlyList<LogFileDescriptor>>> ListFilesAsync(CancellationToken cancellationToken = default)
    {
        return logFileQueryService.ListFilesAsync(cancellationToken);
    }

    /// <summary>
    /// Open the specified log file
    /// </summary>
    public Task<Res<FileStream>> OpenFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        return logFileQueryService.OpenFileAsync(relativePath, cancellationToken);
    }

    /// <summary>
    /// Switch to the specified log file
    /// </summary>
    /// <param name="relativePath">File path relative to the log directory</param>
    /// <param name="initialLines">Number of initially loaded lines</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    /// <returns>Log snapshot of new file</returns>
    public async Task<Res<ScreenLogSnapshot>> SwitchToFileAsync(string relativePath, int initialLines, CancellationToken cancellationToken = default)
    {
        // Pause current listening
        await PauseAsync();

        // Parse full path and switch
        var fullPath = logFileQueryService.ResolveFilePath(relativePath);
        logTailService.SwitchToFile(fullPath);

        // Reinitialize
        return await InitializeAsync(initialLines, cancellationToken);
    }

    /// <summary>
    /// Create a log subscription
    /// </summary>
    public LoggingSubscription Subscribe()
    {
        // Use bounded Channel to prevent unlimited memory growth
        var channel = Channel.CreateBounded<ScreenLogSnapshot>(new BoundedChannelOptions(100)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        lock (_subscriberLock)
        {
            _subscribers.Add(channel);
        }

        var subscription = new LoggingSubscription(this, channel);
        var snapshot = buffer.Snapshot();
        channel.Writer.TryWrite(snapshot);
        return subscription;
    }

    internal void RemoveSubscriber(Channel<ScreenLogSnapshot> channel)
    {
        lock (_subscriberLock)
        {
            _subscribers.Remove(channel);
        }
        // Always try to complete the channel, ensuring resources are released
        channel.Writer.TryComplete();
    }

    private async Task TailLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var line in logTailService.WatchAsync(cancellationToken))
            {
                var snapshot = buffer.Append(line);
                await PublishSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "日志监听任务发生异常");
        }
        finally
        {
            _isRunning = false;
        }
    }

    private async Task PublishSnapshotAsync(ScreenLogSnapshot snapshot, CancellationToken cancellationToken)
    {
        Channel<ScreenLogSnapshot>[] subscribers;
        lock (_subscriberLock)
        {
            if (_subscribers.Count == 0)
            {
                return;
            }

            subscribers = _subscribers.ToArray();
        }

        foreach (var subscriber in subscribers)
        {
            try
            {
                if (!subscriber.Writer.TryWrite(snapshot))
                {
                    await subscriber.Writer.WriteAsync(snapshot, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (ChannelClosedException)
            {
                RemoveSubscriber(subscriber);
            }
        }
    }
}

/// <summary>
/// Log subscription packaging
/// </summary>
public sealed class LoggingSubscription : IAsyncDisposable
{
    private readonly LoggingService _loggingService;
    private readonly Channel<ScreenLogSnapshot> _channel;
    private bool _disposed;

    internal LoggingSubscription(LoggingService loggingService, Channel<ScreenLogSnapshot> channel)
    {
        _loggingService = loggingService;
        _channel = channel;
    }

    public ChannelReader<ScreenLogSnapshot> Reader => _channel.Reader;

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _loggingService.RemoveSubscriber(_channel);
        return ValueTask.CompletedTask;
    }
}
