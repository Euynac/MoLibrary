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
} 