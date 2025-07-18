using Microsoft.Extensions.Logging;

namespace MoLibrary.Core.Features.MoLogProvider;

/// <summary>
/// Default implementation of IMoLogProvider that uses Microsoft's console logger.
/// </summary>
public class ConsoleLogProvider : IMoLogProvider
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLogProvider"/> class.
    /// </summary>
    public ConsoleLogProvider()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });
    }

    /// <inheritdoc />
    public ILogger<T> CreateLogger<T>()
    {
        return _loggerFactory.CreateLogger<T>();
    }

    /// <inheritdoc />
    public ILogger CreateLogger(Type type)
    {
        return _loggerFactory.CreateLogger(type);
    }
    
    /// <inheritdoc />
    public ILogger<T> CreateLogger<T>(LogLevel minLogLevel)
    {
        var logger = _loggerFactory.CreateLogger<T>();
        return new MinimumLevelLogger<T>(logger, minLogLevel);
    }
    
    /// <inheritdoc />
    public ILogger CreateLogger(Type type, LogLevel minLogLevel)
    {
        var logger = _loggerFactory.CreateLogger(type);
        return new MinimumLevelLogger(logger, minLogLevel);
    }
}