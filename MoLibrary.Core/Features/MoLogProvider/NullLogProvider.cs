using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MoLibrary.Core.Features.MoLogProvider;

/// <summary>
/// Implementation of IMoLogProvider that discards all logs (doesn't log anything).
/// </summary>
public class NullLogProvider : IMoLogProvider
{
    private readonly NullLoggerFactory _loggerFactory = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="NullLogProvider"/> class.
    /// </summary>
    public NullLogProvider()
    {
    }

    /// <inheritdoc />
    public ILogger CreateLogger<T>()
    {
        return _loggerFactory.CreateLogger<T>();
    }

    /// <inheritdoc />
    public ILogger CreateLogger(Type type)
    {
        return _loggerFactory.CreateLogger(type);
    }
    
    /// <inheritdoc />
    public ILogger CreateLogger<T>(LogLevel minLogLevel)
    {
        // For NullLogProvider, we ignore the minimum log level as it doesn't log anything anyway
        return _loggerFactory.CreateLogger<T>();
    }
    
    /// <inheritdoc />
    public ILogger CreateLogger(Type type, LogLevel minLogLevel)
    {
        // For NullLogProvider, we ignore the minimum log level as it doesn't log anything anyway
        return _loggerFactory.CreateLogger(type);
    }
} 