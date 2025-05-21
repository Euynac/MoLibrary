using Microsoft.Extensions.Logging;

namespace MoLibrary.Core.Features.MoLogProvider;

/// <summary>
/// Defines the contract for a log provider that can create logger instances.
/// </summary>
public interface IMoLogProvider
{
    /// <summary>
    /// Creates a logger instance for the specified type.
    /// </summary>
    /// <typeparam name="T">The type requesting the logger.</typeparam>
    /// <returns>An ILogger instance for the specified type.</returns>
    ILogger CreateLogger<T>();

    /// <summary>
    /// Creates a logger instance for the specified type.
    /// </summary>
    /// <param name="type">The type requesting the logger.</param>
    /// <returns>An ILogger instance for the specified type.</returns>
    ILogger CreateLogger(Type type);
    
    /// <summary>
    /// Creates a logger instance for the specified type with a minimum log level.
    /// </summary>
    /// <typeparam name="T">The type requesting the logger.</typeparam>
    /// <param name="minLogLevel">The minimum log level to display.</param>
    /// <returns>An ILogger instance for the specified type with the specified minimum log level.</returns>
    ILogger CreateLogger<T>(LogLevel minLogLevel);
    
    /// <summary>
    /// Creates a logger instance for the specified type with a minimum log level.
    /// </summary>
    /// <param name="type">The type requesting the logger.</param>
    /// <param name="minLogLevel">The minimum log level to display.</param>
    /// <returns>An ILogger instance for the specified type with the specified minimum log level.</returns>
    ILogger CreateLogger(Type type, LogLevel minLogLevel);
} 