using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Monica.Core.Logging;

/// <summary>
/// Provides access to logger creation across Monica without introducing a custom provider abstraction.
/// </summary>
public static class LogManager
{
    private static readonly ILoggerFactory DefaultFactory = LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Trace);
        builder.AddConsole();
    });

    private static ILoggerFactory _factory = DefaultFactory;

    /// <summary>
    /// Gets the current logger factory.
    /// </summary>
    public static ILoggerFactory Factory => _factory;

    /// <summary>
    /// Replaces the current logger factory.
    /// </summary>
    /// <param name="factory">The factory to use for future logger creation.</param>
    public static void UseFactory(ILoggerFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Replaces the current logger factory with a no-op implementation.
    /// </summary>
    public static void UseNullFactory()
    {
        _factory = NullLoggerFactory.Instance;
    }

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    public static ILogger<T> For<T>()
    {
        return _factory.CreateLogger<T>();
    }

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    public static ILogger For(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _factory.CreateLogger(type);
    }

    /// <summary>
    /// Creates a logger for the specified category name.
    /// </summary>
    public static ILogger For(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return _factory.CreateLogger(categoryName);
    }

    /// <summary>
    /// Creates a logger for the specified type with a minimum log level gate.
    /// </summary>
    public static ILogger For<T>(LogLevel minLogLevel)
    {
        return new MinimumLevelLogger<T>(_factory.CreateLogger<T>(), minLogLevel);
    }

    /// <summary>
    /// Creates a logger for the specified type with a minimum log level gate.
    /// </summary>
    public static ILogger For(Type type, LogLevel minLogLevel)
    {
        ArgumentNullException.ThrowIfNull(type);
        return new MinimumLevelLogger(_factory.CreateLogger(type), minLogLevel);
    }

    /// <summary>
    /// Creates a logger for the specified category name with a minimum log level gate.
    /// </summary>
    public static ILogger For(string categoryName, LogLevel minLogLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return new MinimumLevelLogger(_factory.CreateLogger(categoryName), minLogLevel);
    }
}
