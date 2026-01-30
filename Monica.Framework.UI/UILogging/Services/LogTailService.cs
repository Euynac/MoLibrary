using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Framework.UI.Modules;
using Monica.Logging.Modules;
using Monica.Tool.MoResponse;
using Monica.Framework.UI.UILogging.Models;

namespace Monica.Framework.UI.UILogging.Services;

/// <summary>
/// 日志读取结果
/// </summary>
/// <param name="Lines">日志行集合</param>
/// <param name="StartLineNumber">起始行号（第一行的绝对行号）</param>
public readonly record struct LogReadResult(IReadOnlyList<string> Lines, long StartLineNumber);

/// <summary>
/// 底层日志文件读取服务
/// </summary>
public sealed class LogTailService(
    IOptions<ModuleLoggingOption> loggingOptions,
    IOptions<ModuleLoggingUIOption> uiOptions,
    ILogger<LogTailService> logger)
{
    private string _logFilePath = ResolveLogFilePath(loggingOptions.Value, uiOptions.Value);
    private readonly TimeSpan _pollingInterval = uiOptions.Value.PollingInterval;
    private readonly object _syncRoot = new();

    private long _lastPosition;
    private long _currentLineNumber;
    private bool _isInitialized;

    public string LogFilePath => _logFilePath;

    /// <summary>
    /// 当前日志文件是否存在
    /// </summary>
    public bool LogFileExists => File.Exists(_logFilePath);

    public string LogDirectory => Path.GetDirectoryName(_logFilePath) ?? AppContext.BaseDirectory;

    /// <summary>
    /// 当前行号（最后读取到的行的绝对行号）
    /// </summary>
    public long CurrentLineNumber
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentLineNumber;
            }
        }
    }

    /// <summary>
    /// 文件总行数（初始化时统计）
    /// </summary>
    public long TotalFileLineCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentLineNumber;
            }
        }
    }

    /// <summary>
    /// 切换到新的日志文件
    /// </summary>
    /// <param name="newFilePath">新的日志文件完整路径</param>
    public void SwitchToFile(string newFilePath)
    {
        lock (_syncRoot)
        {
            _logFilePath = newFilePath;
            _lastPosition = 0;
            _currentLineNumber = 0;
            _isInitialized = false;
        }

        logger.LogInformation("已切换到日志文件: {FilePath}", newFilePath);
    }

    /// <summary>
    /// 读取指定行号之前的N行日志
    /// </summary>
    /// <param name="beforeLineNumber">在此行号之前读取</param>
    /// <param name="lineCount">要读取的行数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>读取的日志行及起始行号</returns>
    public async Task<Res<LogReadResult>> ReadLinesBeforeAsync(long beforeLineNumber, int lineCount, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_logFilePath))
            {
                logger.LogWarning("日志文件不存在: {File}", _logFilePath);
                return new LogReadResult(Array.Empty<string>(), 0);
            }

            var lines = new List<string>();
            var startLineNumber = Math.Max(1, beforeLineNumber - lineCount);
            var endLineNumber = beforeLineNumber - 1;

            if (endLineNumber < startLineNumber)
            {
                return new LogReadResult(Array.Empty<string>(), startLineNumber);
            }

            await using var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            long currentLine = 1;
            while (!reader.EndOfStream && currentLine <= endLineNumber)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync();
                if (line == null)
                {
                    break;
                }

                if (currentLine >= startLineNumber)
                {
                    lines.Add(line);
                }

                currentLine++;
            }

            return new LogReadResult(lines, startLineNumber);
        }
        catch (OperationCanceledException)
        {
            return new LogReadResult(Array.Empty<string>(), 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "读取指定行之前的日志失败");
            return $"读取日志失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 读取最近的N行日志
    /// </summary>
    public async Task<Res<LogReadResult>> ReadLatestLinesAsync(int lineCount, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_logFilePath))
            {
                logger.LogWarning("日志文件不存在: {File}", _logFilePath);
                lock (_syncRoot)
                {
                    _lastPosition = 0;
                    _currentLineNumber = 0;
                    _isInitialized = true;
                }

                return new LogReadResult(Array.Empty<string>(), 0);
            }

            var queue = new Queue<string>(lineCount);
            long totalLineCount = 0;

            await using var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync();
                if (line == null)
                {
                    break;
                }

                totalLineCount++;

                if (queue.Count == lineCount)
                {
                    queue.Dequeue();
                }

                queue.Enqueue(line);
            }

            var startLineNumber = Math.Max(1, totalLineCount - queue.Count + 1);

            lock (_syncRoot)
            {
                _lastPosition = stream.Position;
                _currentLineNumber = totalLineCount;
                _isInitialized = true;
            }

            return new LogReadResult(queue.ToList(), startLineNumber);
        }
        catch (OperationCanceledException)
        {
            return new LogReadResult(Array.Empty<string>(), 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "读取日志文件失败");
            return $"读取日志文件失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 监听日志文件新增内容
    /// </summary>
    public async IAsyncEnumerable<string> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnsureInitialized();

        while (!cancellationToken.IsCancellationRequested)
        {
            List<string> newLines;
            try
            {
                newLines = await ReadNewLinesInternalAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "监听日志文件发生异常");
                await Task.Delay(_pollingInterval, cancellationToken);
                continue;
            }

            if (newLines.Count == 0)
            {
                await Task.Delay(_pollingInterval, cancellationToken);
                continue;
            }

            foreach (var line in newLines)
            {
                if (line != null)
                {
                    yield return line;
                }
            }
        }
    }

    private async Task<List<string>> ReadNewLinesInternalAsync(CancellationToken cancellationToken)
    {
        var result = new List<string>();

        if (!File.Exists(_logFilePath))
        {
            return result;
        }

        await using var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        long startPosition;
        bool isFileRotated = false;
        lock (_syncRoot)
        {
            // 检测文件是否被轮转（文件大小小于上次位置）
            if (stream.Length < _lastPosition)
            {
                _lastPosition = 0;
                _currentLineNumber = 0;
                isFileRotated = true;
            }

            startPosition = _lastPosition;
        }

        if (startPosition > stream.Length)
        {
            startPosition = 0;
            isFileRotated = true;
        }

        stream.Seek(startPosition, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            line = await reader.ReadLineAsync();
            if (line == null)
            {
                break;
            }

            result.Add(line);
        }

        lock (_syncRoot)
        {
            _lastPosition = stream.Position;
            // 增加行号计数
            _currentLineNumber += result.Count;
        }

        if (isFileRotated && result.Count > 0)
        {
            logger.LogInformation("检测到日志文件轮转，行号已重置");
        }

        return result;
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                if (File.Exists(_logFilePath))
                {
                    var info = new FileInfo(_logFilePath);
                    _lastPosition = info.Length;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "初始化日志文件位置失败");
                _lastPosition = 0;
            }
            finally
            {
                _isInitialized = true;
            }
        }
    }

    private static string ResolveLogFilePath(ModuleLoggingOption option, ModuleLoggingUIOption uiOption)
    {
        if (!string.IsNullOrWhiteSpace(uiOption.LogDirectory))
        {
            var directory = Path.GetFullPath(uiOption.LogDirectory);
            var fileName = !string.IsNullOrWhiteSpace(option.LogFilePath)
                ? Path.GetFileName(option.LogFilePath)
                : option.LogFileName;
            return Path.Combine(directory, fileName);
        }

        if (!string.IsNullOrWhiteSpace(option.LogFilePath))
        {
            return Path.GetFullPath(option.LogFilePath);
        }

        {
            var directory = option.LogFileDirectory ?? Path.Combine(AppContext.BaseDirectory, "Logs");
            var logDirectory = Path.GetFullPath(directory);
            return Path.Combine(logDirectory, option.LogFileName);
        }
        
    }
}
