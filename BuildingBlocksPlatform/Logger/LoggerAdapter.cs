using MediatR;

namespace BuildingBlocksPlatform.SeedWork;

public class LoggerAdapter<T> : ILogger<T>
{
    private readonly ILogger _logger;

    public LoggerAdapter(ILogger logger)
    {
        _logger=logger;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);


    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logger.Log(logLevel, eventId, state, exception, formatter);
    }
}