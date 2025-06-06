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
    public ILogger CreateLogger<T>()
    {
        return _provider.CreateLogger(typeof(T).FullName ?? typeof(T).Name);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(Type type)
    {
        return _provider.CreateLogger(type.FullName ?? type.Name);
    }
    
    /// <inheritdoc />
    public ILogger CreateLogger<T>(LogLevel minLogLevel)
    {
        var logger = CreateLogger<T>();
        return new MinimumLevelLogger(logger, minLogLevel);
    }
    
    /// <inheritdoc />
    public ILogger CreateLogger(Type type, LogLevel minLogLevel)
    {
        var logger = CreateLogger(type);
        return new MinimumLevelLogger(logger, minLogLevel);
    }
}