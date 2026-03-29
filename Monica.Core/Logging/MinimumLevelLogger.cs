using Microsoft.Extensions.Logging;

namespace Monica.Core.Logging;

/// <summary>
/// Logger that filters logs based on a minimum log level.
/// </summary>
public class MinimumLevelLogger(ILogger innerLogger, LogLevel minLogLevel) : ILogger
{
    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        innerLogger.Log(logLevel, eventId, state, exception, formatter);
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= minLogLevel && innerLogger.IsEnabled(logLevel);
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return innerLogger.BeginScope(state);
    }
}

/// <summary>
/// Generic logger that filters logs based on a minimum log level.
/// </summary>
public class MinimumLevelLogger<T>(ILogger<T> innerLogger, LogLevel minLogLevel) : ILogger<T>
{
    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        innerLogger.Log(logLevel, eventId, state, exception, formatter);
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= minLogLevel && innerLogger.IsEnabled(logLevel);
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return innerLogger.BeginScope(state);
    }
}
