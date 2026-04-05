using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Results;
using Monica.Framework.UI.UILogging.Models;
using Monica.Modules;

namespace Monica.Framework.UI.UILogging.Support;

/// <summary>
/// Log file query and download service
/// </summary>
public sealed class LogFileQueryService(
    IOptions<ModuleLoggingOption> loggingOptions,
    IOptions<ModuleLoggingUIOption> uiOptions,
    ILogger<LogFileQueryService> logger)
{
    private readonly string _logDirectory = ResolveDirectory(loggingOptions.Value, uiOptions.Value);

    public string LogDirectory => _logDirectory;

    public Task<Res<IReadOnlyList<LogFileDescriptor>>> ListFilesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                return Task.FromResult((Res<IReadOnlyList<LogFileDescriptor>>)Array.Empty<LogFileDescriptor>());
            }

            var directory = new DirectoryInfo(_logDirectory);
            var files = directory
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f =>
                {
                    var relativePath = Path.GetRelativePath(_logDirectory, f.FullName)
                        .Replace(Path.DirectorySeparatorChar, '/');

                    return new LogFileDescriptor(
                        f.Name,
                        f.Length,
                        f.LastWriteTime,
                        relativePath);
                })
                .ToList();

            return Task.FromResult((Res<IReadOnlyList<LogFileDescriptor>>)files);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "读取日志目录失败");
            return Task.FromResult((Res<IReadOnlyList<LogFileDescriptor>>) $"读取日志目录失败: {ex.Message}");
        }
    }

    public Task<Res<FileStream>> OpenFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = ResolveFilePath(relativePath);
            if (!File.Exists(fullPath))
            {
                return Task.FromResult((Res<FileStream>) $"指定日志文件不存在: {relativePath}");
            }

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Task.FromResult((Res<FileStream>)stream);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "打开日志文件失败");
            return Task.FromResult((Res<FileStream>) $"打开日志文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Read the context log around the specified line number
    /// </summary>
    /// <param name="filePath">Log file path (absolute path)</param>
    /// <param name="targetLineNumber">Target line number (absolute line number, starting from 1)</param>
    /// <param name="contextLines">Number of context lines (how many lines are read before and after)</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    /// <returns>List of log view models containing the target row and its context</returns>
    public async Task<Res<ContextLogResult>> ReadContextAsync(
        string filePath,
        long targetLineNumber,
        int contextLines = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return $"日志文件不存在: {filePath}";
            }

            var startLine = Math.Max(1, targetLineNumber - contextLines);
            var endLine = targetLineNumber + contextLines;

            var lines = new List<LogLineViewModel>();
            long currentLineNumber = 1;

            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (currentLineNumber <= endLine)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rawLine = await reader.ReadLineAsync();
                if (rawLine is null)
                {
                    break;
                }

                if (currentLineNumber >= startLine)
                {
                    var viewModel = LogLineViewModel.FromRaw(rawLine);
                    viewModel.WithPosition(currentLineNumber, null);

                    if (currentLineNumber == targetLineNumber)
                    {
                        viewModel.WithContextTarget(true);
                    }

                    lines.Add(viewModel);
                }

                currentLineNumber++;
            }

            if (lines.Count == 0)
            {
                return $"未找到行号 {targetLineNumber} 附近的日志内容";
            }

            var result = new ContextLogResult(lines, targetLineNumber, startLine, currentLineNumber - 1);
            return result;
        }
        catch (OperationCanceledException)
        {
            return "读取上下文日志被取消";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "读取上下文日志失败，目标行号: {LineNumber}", targetLineNumber);
            return $"读取上下文日志失败: {ex.Message}";
        }
    }

    /// <summary>
    /// Resolve relative path to full log file path
    /// </summary>
    /// <param name="relativePath">Path relative to the log directory</param>
    /// <returns>Full file path</returns>
    /// <exception cref="InvalidOperationException">Thrown when the path attempts to access a location outside the log directory</exception>
    public string ResolveFilePath(string relativePath)
    {
        var combined = Path.Combine(_logDirectory, relativePath);
        var fullPath = Path.GetFullPath(combined);
        var directoryPath = Path.GetFullPath(_logDirectory);

        if (!fullPath.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("非法的日志文件访问路径");
        }

        return fullPath;
    }

    private static string ResolveDirectory(ModuleLoggingOption option, ModuleLoggingUIOption uiOption)
    {
        if (!string.IsNullOrWhiteSpace(uiOption.LogDirectory))
        {
            return Path.GetFullPath(uiOption.LogDirectory);
        }

        if (!string.IsNullOrWhiteSpace(option.LogFilePath))
        {
            var fullPath = Path.GetFullPath(option.LogFilePath);
            var directory = Path.GetDirectoryName(fullPath);
            return directory ?? AppContext.BaseDirectory;
        }

        var fallbackDirectory = option.LogDirectory ?? Path.Combine(AppContext.BaseDirectory, "Logs");
        return Path.GetFullPath(fallbackDirectory);
    }
}
