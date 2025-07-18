using Microsoft.Extensions.Logging;

namespace MoLibrary.Core.Features.MoLogProvider;

/// <summary>
/// Logger that filters logs based on a minimum log level.
/// </summary>
public class MinimumLevelLogger : ILogger
{
    private readonly ILogger _innerLogger;
    private readonly LogLevel _minLogLevel;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MinimumLevelLogger"/> class.
    /// </summary>
    /// <param name="innerLogger">The underlying logger to use.</param>
    /// <param name="minLogLevel">The minimum log level to display.</param>
    public MinimumLevelLogger(ILogger innerLogger, LogLevel minLogLevel)
    {
        _innerLogger = innerLogger;
        _minLogLevel = minLogLevel;
    }
    
    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;
            
        _innerLogger.Log(logLevel, eventId, state, exception, formatter);
    }
    
    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _minLogLevel && _innerLogger.IsEnabled(logLevel);
    }
    
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _innerLogger.BeginScope(state);
    }
}

/// <summary>
/// Generic logger that filters logs based on a minimum log level.
/// </summary>
/// <typeparam name="T">The type for which the logger is created.</typeparam>
public class MinimumLevelLogger<T> : ILogger<T>
{
    private readonly ILogger<T> _innerLogger;
    private readonly LogLevel _minLogLevel;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MinimumLevelLogger{T}"/> class.
    /// </summary>
    /// <param name="innerLogger">The underlying logger to use.</param>
    /// <param name="minLogLevel">The minimum log level to display.</param>
    public MinimumLevelLogger(ILogger<T> innerLogger, LogLevel minLogLevel)
    {
        _innerLogger = innerLogger;
        _minLogLevel = minLogLevel;
    }
    
    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;
            
        _innerLogger.Log(logLevel, eventId, state, exception, formatter);
    }
    
    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _minLogLevel && _innerLogger.IsEnabled(logLevel);
    }
    
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _innerLogger.BeginScope(state);
    }
}