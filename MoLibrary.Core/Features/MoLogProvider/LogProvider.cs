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
} 