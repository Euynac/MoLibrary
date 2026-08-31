using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Monica.Guide;

/// <summary>Options for bounded local product logs.</summary>
public sealed class GuideRollingFileLoggerOptions
{
    public string DirectoryPath { get; set; } = string.Empty;

    public string FileName { get; set; } = "workflow.log";

    public long MaxFileBytes { get; set; } = 4 * 1024 * 1024;

    public int RetainedFileCount { get; set; } = 5;

    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
}

/// <summary>Small dependency-free rolling <see cref="ILoggerProvider"/> for local diagnostics.</summary>
public sealed class GuideRollingFileLoggerProvider : ILoggerProvider
{
    private readonly GuideRollingFileLoggerOptions _options;
    private readonly object _syncRoot = new();
    private bool _disposed;

    public GuideRollingFileLoggerProvider(IOptions<GuideRollingFileLoggerOptions> options)
        : this(options.Value)
    {
    }

    public GuideRollingFileLoggerProvider(GuideRollingFileLoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FileName);
        if (options.MaxFileBytes < 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxFileBytes must be at least 1024 bytes.");
        }

        if (options.RetainedFileCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "RetainedFileCount must be positive.");
        }

        _options = options;
    }

    public ILogger CreateLogger(string categoryName)
        => new RollingLogger(this, categoryName);

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _disposed = true;
        }
    }

    private void Write(
        string category,
        LogLevel level,
        EventId eventId,
        string message,
        Exception? exception)
    {
        if (level < _options.MinimumLevel)
        {
            return;
        }

        var line = JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            level = level.ToString(),
            category,
            eventId = eventId.Id,
            message,
            exception = exception?.ToString()
        }) + Environment.NewLine;

        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Directory.CreateDirectory(_options.DirectoryPath);
            var current = Path.Combine(_options.DirectoryPath, _options.FileName);
            var additionalBytes = System.Text.Encoding.UTF8.GetByteCount(line);
            if (File.Exists(current) && new FileInfo(current).Length + additionalBytes > _options.MaxFileBytes)
            {
                Roll(current);
            }

            File.AppendAllText(current, line, System.Text.Encoding.UTF8);
        }
    }

    private void Roll(string current)
    {
        var oldest = $"{current}.{_options.RetainedFileCount}";
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var index = _options.RetainedFileCount - 1; index >= 1; index--)
        {
            var source = $"{current}.{index}";
            if (File.Exists(source))
            {
                File.Move(source, $"{current}.{index + 1}");
            }
        }

        File.Move(current, $"{current}.1");
    }

    private sealed class RollingLogger(
        GuideRollingFileLoggerProvider provider,
        string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= provider._options.MinimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (IsEnabled(logLevel))
            {
                provider.Write(category, logLevel, eventId, formatter(state, exception), exception);
            }
        }
    }
}

/// <summary>Logging registration for bounded guide-managed product logs.</summary>
public static class GuideProductLoggingExtensions
{
    public static ILoggingBuilder AddGuideProductFile(
        this ILoggingBuilder builder,
        AgentProductPaths paths,
        Action<GuideRollingFileLoggerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(paths);
        builder.Services.Configure<GuideRollingFileLoggerOptions>(options =>
        {
            options.DirectoryPath = paths.LogsDirectory;
            configure?.Invoke(options);
        });
        builder.Services.AddSingleton<ILoggerProvider, GuideRollingFileLoggerProvider>();
        return builder;
    }
}
