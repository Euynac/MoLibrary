using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Monica.Core.Logging;

/// <summary>
/// Owns the replaceable logger factory used while one Monica host is being composed.
/// </summary>
internal sealed class RegistrationLoggerFactory : ILoggerFactory
{
    private readonly ConcurrentDictionary<string, VersionedLogger> _loggers = new(StringComparer.Ordinal);
    private FactoryState _state = new(CreateBootstrapFactory(), OwnsFactory: true, Version: 0);
    private bool _disposed;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return new ReloadableLogger(this, categoryName);
    }

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(provider);
        Volatile.Read(ref _state).Factory.AddProvider(provider);
    }

    /// <summary>
    /// Replaces the current registration logger factory for this host.
    /// Existing loggers resolve against the replacement on their next operation.
    /// </summary>
    /// <param name="factory">The host-owned logger factory to use.</param>
    /// <param name="ownsFactory">Whether this instance should dispose the replacement.</param>
    public void Replace(ILoggerFactory factory, bool ownsFactory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(factory);

        var previous = Volatile.Read(ref _state);
        var replacement = new FactoryState(factory, ownsFactory, previous.Version + 1);
        Volatile.Write(ref _state, replacement);
        _loggers.Clear();

        if (previous.OwnsFactory)
        {
            previous.Factory.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var state = Volatile.Read(ref _state);
        _loggers.Clear();
        if (state.OwnsFactory)
        {
            state.Factory.Dispose();
        }
    }

    private ILogger GetCurrentLogger(string categoryName)
    {
        var state = Volatile.Read(ref _state);
        var versionedLogger = _loggers.AddOrUpdate(
            categoryName,
            _ => new VersionedLogger(state.Version, state.Factory.CreateLogger(categoryName)),
            (_, existing) => existing.Version == state.Version
                ? existing
                : new VersionedLogger(state.Version, state.Factory.CreateLogger(categoryName)));
        return versionedLogger.Logger;
    }

    private static ILoggerFactory CreateBootstrapFactory()
    {
        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddConsole();
        });
    }

    private sealed record FactoryState(ILoggerFactory Factory, bool OwnsFactory, long Version);

    private sealed record VersionedLogger(long Version, ILogger Logger);

    private sealed class ReloadableLogger(RegistrationLoggerFactory owner, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return owner.GetCurrentLogger(categoryName).BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return owner.GetCurrentLogger(categoryName).IsEnabled(logLevel);
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            owner.GetCurrentLogger(categoryName).Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
