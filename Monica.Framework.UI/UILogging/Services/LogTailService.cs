using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.Tool.Results;

namespace Monica.Framework.UI.UILogging.Services;

/// <summary>
/// Log reading results
/// </summary>
/// <param name="Lines">Log line collection</param>
/// <param name="StartLineNumber">Start line number (absolute line number of the first line)</param>
public readonly record struct LogReadResult(IReadOnlyList<string> Lines, long StartLineNumber);

/// <summary>
/// Underlying log file reading service
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
    /// Does the current log file exist?
    /// </summary>
    public bool LogFileExists => File.Exists(_logFilePath);

    public string LogDirectory => Path.GetDirectoryName(_logFilePath) ?? AppContext.BaseDirectory;

    /// <summary>
    /// Current line number (absolute line number of the last line read)
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
    /// Total number of lines in the file (statistics during initialization)
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
    /// Switch to new log file
    /// </summary>
    /// <param name="newFilePath">New log file full path</param>
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
    /// Read N lines of logs before the specified line number
    /// </summary>
    /// <param name="beforeLineNumber">Read before this line number</param>
    /// <param name="lineCount">Number of lines to read</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    /// <returns>The log lines read and the starting line number</returns>
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
            while (currentLine <= endLineNumber)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync();
                if (line is null)
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
    /// Read the most recent N lines of logs
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

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync();
                if (line is null)
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
    /// Monitor new content in log files
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
            // Detect if a file is rotated (file size is smaller than last position)
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

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            result.Add(line);
        }

        lock (_syncRoot)
        {
            _lastPosition = stream.Position;
            // Increase line number count
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
            var directory = option.LogDirectory ?? Path.Combine(AppContext.BaseDirectory, "Logs");
            var logDirectory = Path.GetFullPath(directory);
            return Path.Combine(logDirectory, option.LogFileName);
        }
        
    }
}
