using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.Framework.UI.UILogging.Models;
using Monica.Tool.Extensions;
using Monica.Tool.MoResponse;

namespace Monica.Framework.UI.UILogging.Services;

/// <summary>
/// 日志页面核心服务，负责维护临时日志池与实时订阅
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
    /// 当前日志文件是否存在
    /// </summary>
    public bool LogFileExists => logTailService.LogFileExists;

    /// <summary>
    /// 初始化日志缓冲池
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
    /// 启动日志实时监听
    /// </summary>
    public Task<Res> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            return Task.FromResult(Res.Ok("日志监听已在运行"));
        }

        // 确保释放旧的 CancellationTokenSource
        _tailCts.SafeCancelAndDispose();

        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _tailCts = linked;
        _tailTask = Task.Run(() => TailLoopAsync(linked.Token));
        _isRunning = true;
        logger.LogInformation("日志监听任务已启动");
        return Task.FromResult(Res.Ok("日志监听已启动"));
    }

    /// <summary>
    /// 暂停日志监听
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
    /// 更新筛选器
    /// </summary>
    public async Task<Res<ScreenLogSnapshot>> UpdateFilterAsync(string? keyword, bool onlyCapture)
    {
        var filter = LogFilterState.Create(keyword, onlyCapture);
        var snapshot = buffer.ApplyFilter(filter);
        await PublishSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
        return snapshot;
    }

    /// <summary>
    /// 获取当前快照
    /// </summary>
    public ScreenLogSnapshot GetSnapshot()
    {
        return buffer.Snapshot();
    }

    /// <summary>
    /// 加载更多历史日志（在当前缓冲区之前）
    /// </summary>
    /// <param name="lineCount">要加载的行数，默认50行</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>加载的行数</returns>
    public async Task<Res<int>> LoadMoreLinesAsync(int lineCount = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = buffer.Snapshot();
            if (snapshot.Lines.Count == 0)
            {
                return 0;
            }

            // 获取当前缓冲区最早的行号
            var firstLine = snapshot.Lines.FirstOrDefault();
            if (firstLine?.AbsoluteLineNumber == null || firstLine.AbsoluteLineNumber <= 1)
            {
                // 已经到达文件开头
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

            // 前置到缓冲区
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
    /// 导出当前临时日志池
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
    /// 列出日志目录文件
    /// </summary>
    public Task<Res<IReadOnlyList<LogFileDescriptor>>> ListFilesAsync(CancellationToken cancellationToken = default)
    {
        return logFileQueryService.ListFilesAsync(cancellationToken);
    }

    /// <summary>
    /// 打开指定日志文件
    /// </summary>
    public Task<Res<FileStream>> OpenFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        return logFileQueryService.OpenFileAsync(relativePath, cancellationToken);
    }

    /// <summary>
    /// 切换到指定的日志文件
    /// </summary>
    /// <param name="relativePath">相对于日志目录的文件路径</param>
    /// <param name="initialLines">初始加载的行数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新文件的日志快照</returns>
    public async Task<Res<ScreenLogSnapshot>> SwitchToFileAsync(string relativePath, int initialLines, CancellationToken cancellationToken = default)
    {
        // 暂停当前监听
        await PauseAsync();

        // 解析完整路径并切换
        var fullPath = logFileQueryService.ResolveFilePath(relativePath);
        logTailService.SwitchToFile(fullPath);

        // 重新初始化
        return await InitializeAsync(initialLines, cancellationToken);
    }

    /// <summary>
    /// 创建日志订阅
    /// </summary>
    public LoggingSubscription Subscribe()
    {
        // 使用有界 Channel，防止内存无限增长
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
        // 总是尝试完成 channel，确保资源释放
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
/// 日志订阅包装
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
