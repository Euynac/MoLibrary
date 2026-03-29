using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Monica.Core.Logging;

/// <summary>
/// Provides access to logger creation across Monica without introducing a custom provider abstraction.
/// </summary>
public static class LogManager
{
    private static readonly ILoggerFactory _defaultFactory = LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Trace);
        builder.AddConsole();
    });

    /// <summary>
    /// Gets the current logger factory.
    /// </summary>
    public static ILoggerFactory Factory { get; private set; } = _defaultFactory;

    /// <summary>
    /// Replaces the current logger factory.
    /// </summary>
    /// <param name="factory">The factory to use for future logger creation.</param>
    public static void UseFactory(ILoggerFactory factory)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Replaces the current logger factory with a no-op implementation.
    /// </summary>
    public static void UseNullFactory()
    {
        Factory = NullLoggerFactory.Instance;
    }

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    public static ILogger<T> For<T>()
    {
        return Factory.CreateLogger<T>();
    }

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    public static ILogger For(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Factory.CreateLogger(type);
    }

    /// <summary>
    /// Creates a logger for the specified category name.
    /// </summary>
    public static ILogger For(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return Factory.CreateLogger(categoryName);
    }

    /// <summary>
    /// Creates a logger for the specified type with a minimum log level gate.
    /// </summary>
    public static ILogger For<T>(LogLevel minLogLevel)
    {
        return new MinimumLevelLogger<T>(Factory.CreateLogger<T>(), minLogLevel);
    }

    /// <summary>
    /// Creates a logger for the specified type with a minimum log level gate.
    /// </summary>
    public static ILogger For(Type type, LogLevel minLogLevel)
    {
        ArgumentNullException.ThrowIfNull(type);
        return new MinimumLevelLogger(Factory.CreateLogger(type), minLogLevel);
    }

    /// <summary>
    /// Creates a logger for the specified category name with a minimum log level gate.
    /// </summary>
    public static ILogger For(string categoryName, LogLevel minLogLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return new MinimumLevelLogger(Factory.CreateLogger(categoryName), minLogLevel);
    }
}
