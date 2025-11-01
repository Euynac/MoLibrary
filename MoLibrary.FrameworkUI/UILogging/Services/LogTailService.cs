using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.FrameworkUI.Modules;
using MoLibrary.Logging.Modules;
using MoLibrary.Tool.MoResponse;
using MoLibrary.FrameworkUI.UILogging.Models;

namespace MoLibrary.FrameworkUI.UILogging.Services;

/// <summary>
/// 底层日志文件读取服务
/// </summary>
public sealed class LogTailService(
    IOptions<ModuleLoggingOption> loggingOptions,
    IOptions<ModuleLoggingUIOption> uiOptions,
    ILogger<LogTailService> logger)
{
    private readonly string _logFilePath = ResolveLogFilePath(loggingOptions.Value, uiOptions.Value);
    private readonly TimeSpan _pollingInterval = uiOptions.Value.PollingInterval;
    private readonly object _syncRoot = new();

    private long _lastPosition;
    private bool _isInitialized;

    public string LogFilePath => _logFilePath;

    public string LogDirectory => Path.GetDirectoryName(_logFilePath) ?? AppContext.BaseDirectory;

    /// <summary>
    /// 读取最近的N行日志
    /// </summary>
    public async Task<Res<IReadOnlyList<string>>> ReadLatestLinesAsync(int lineCount, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_logFilePath))
            {
                logger.LogWarning("日志文件不存在: {File}", _logFilePath);
                lock (_syncRoot)
                {
                    _lastPosition = 0;
                    _isInitialized = true;
                }

                return Array.Empty<string>();
            }

            var queue = new Queue<string>(lineCount);

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

                if (queue.Count == lineCount)
                {
                    queue.Dequeue();
                }

                queue.Enqueue(line);
            }

            lock (_syncRoot)
            {
                _lastPosition = stream.Position;
                _isInitialized = true;
            }

            return queue.ToList();
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<string>();
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
        lock (_syncRoot)
        {
            if (stream.Length < _lastPosition)
            {
                _lastPosition = 0;
            }

            startPosition = _lastPosition;
        }

        if (startPosition > stream.Length)
        {
            startPosition = 0;
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
