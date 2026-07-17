using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Monica.Testing.Hosting;

/// <summary>
/// Registers an xUnit output logger provider for the active test context.
/// </summary>
public static class XunitTestOutputLoggerServiceCollectionExtensions
{
    /// <summary>
    /// Sends log messages to the active xUnit v3 test output helper when a test is running.
    /// </summary>
    public static IServiceCollection AddXunitTestOutputLogging(this IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddProvider(new XunitTestOutputLoggerProvider()));
        return services;
    }
}

internal sealed class XunitTestOutputLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new XunitTestOutputLogger(categoryName);
    }

    public void Dispose()
    {
    }
}

internal sealed class XunitTestOutputLogger(string categoryName) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        var helper = TestContext.Current.TestOutputHelper;
        if (helper is null)
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        var prefix = eventId.Id == 0
            ? $"{logLevel}: {categoryName}"
            : $"{logLevel}: {categoryName}[{eventId.Id}]";
        helper.WriteLine(exception is null
            ? $"{prefix}: {message}"
            : $"{prefix}: {message}{Environment.NewLine}{exception}");
    }
}
