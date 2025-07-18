using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoLogProvider;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MoLibrary.Logging.ProviderSerilog;

/// <summary>
/// Implementation of IMoLogProvider that uses Serilog for logging.
/// </summary>
public class SerilogProvider : IMoLogProvider
{
    private readonly SerilogLoggerProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerilogProvider"/> class.
    /// </summary>
    /// <param name="logger">The Serilog logger instance to use.</param>
    public SerilogProvider(Serilog.ILogger logger)
    {
        _provider = new SerilogLoggerProvider(logger);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerilogProvider"/> class using the global Serilog logger.
    /// </summary>
    public SerilogProvider() : this(Log.Logger)
    {
    }

    /// <inheritdoc />
    public ILogger<T> CreateLogger<T>()
    {
        var logger = _provider.CreateLogger(typeof(T).FullName ?? typeof(T).Name);
        return new LoggerWrapper<T>(logger);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(Type type)
    {
        return _provider.CreateLogger(type.FullName ?? type.Name);
    }
    
    /// <inheritdoc />
    public ILogger<T> CreateLogger<T>(LogLevel minLogLevel)
    {
        var logger = CreateLogger<T>();
        return new MinimumLevelLogger<T>(logger, minLogLevel);
    }
    
    /// <inheritdoc />
    public ILogger CreateLogger(Type type, LogLevel minLogLevel)
    {
        var logger = CreateLogger(type);
        return new MinimumLevelLogger(logger, minLogLevel);
    }
}

/// <summary>
/// Wrapper class to convert ILogger to ILogger&lt;T&gt;.
/// </summary>
/// <typeparam name="T">The type for which the logger is created.</typeparam>
internal class LoggerWrapper<T> : ILogger<T>
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerWrapper{T}"/> class.
    /// </summary>
    /// <param name="logger">The underlying logger to wrap.</param>
    public LoggerWrapper(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logger.Log(logLevel, eventId, state, exception, formatter);
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return _logger.IsEnabled(logLevel);
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _logger.BeginScope(state);
    }
}