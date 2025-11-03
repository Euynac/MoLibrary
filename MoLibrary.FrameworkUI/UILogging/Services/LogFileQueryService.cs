using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.FrameworkUI.Modules;
using MoLibrary.FrameworkUI.UILogging.Models;
using MoLibrary.Logging.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.FrameworkUI.UILogging.Services;

/// <summary>
/// 日志文件查询与下载服务
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

    private string ResolveFilePath(string relativePath)
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

        var fallbackDirectory = option.LogFileDirectory ?? Path.Combine(AppContext.BaseDirectory, "Logs");
        return Path.GetFullPath(fallbackDirectory);
    }
}
