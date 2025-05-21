using Microsoft.Extensions.Logging;

namespace MoLibrary.Core.Features.MoLogProvider;

/// <summary>
/// Provides access to logging functionality throughout the application.
/// </summary>
public static class LogProvider
{
    private static IMoLogProvider _provider = new ConsoleLogProvider();

    /// <summary>
    /// Gets or sets the current log provider implementation.
    /// </summary>
    public static IMoLogProvider Provider
    {
        get => _provider;
        set => _provider = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Disables all logging by replacing the current provider with a NullLogProvider.
    /// </summary>
    public static void DisableLog()
    {
        _provider = new NullLogProvider();
    }

    /// <summary>
    /// Gets a logger instance for the specified type.
    /// </summary>
    /// <typeparam name="T">The type requesting the logger.</typeparam>
    /// <returns>An ILogger instance for the specified type.</returns>
    public static ILogger For<T>()
    {
        return _provider.CreateLogger<T>();
    }

    /// <summary>
    /// Gets a logger instance for the specified type.
    /// </summary>
    /// <param name="type">The type requesting the logger.</param>
    /// <returns>An ILogger instance for the specified type.</returns>
    public static ILogger For(Type type)
    {
        return _provider.CreateLogger(type);
    }
    
    /// <summary>
    /// Gets a logger instance for the specified type with a minimum log level.
    /// </summary>
    /// <typeparam name="T">The type requesting the logger.</typeparam>
    /// <param name="minLogLevel">The minimum log level to display.</param>
    /// <returns>An ILogger instance for the specified type with the specified minimum log level.</returns>
    public static ILogger For<T>(LogLevel minLogLevel)
    {
        return _provider.CreateLogger<T>(minLogLevel);
    }
    
    /// <summary>
    /// Gets a logger instance for the specified type with a minimum log level.
    /// </summary>
    /// <param name="type">The type requesting the logger.</param>
    /// <param name="minLogLevel">The minimum log level to display.</param>
    /// <returns>An ILogger instance for the specified type with the specified minimum log level.</returns>
    public static ILogger For(Type type, LogLevel minLogLevel)
    {
        return _provider.CreateLogger(type, minLogLevel);
    }
} 