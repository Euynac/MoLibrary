namespace BuildingBlocksPlatform.SeedWork;

/// <summary>
/// 全局Logger，一般用于Platform中的日志记录，日志配置在每个服务的Startup中
/// </summary>
public static class GlobalLog
{
    private static ILogger _logger = null!;

    /// <summary>
    /// Globally shared logger instance.
    /// </summary>
    public static ILogger Logger
    {
        get
        {
            if (_logger == null)
            {
                throw new InvalidOperationException("Global logger has not been configured.");
            }

            return _logger;
        }
        set
        {
            // ReSharper disable once ArrangeAccessorOwnerBody
            _logger = value;
            //var generator = new ProxyGenerator();
            //_logger = generator.CreateInterfaceProxyWithTarget(value, new DebugLogInterceptor());
        }
    }

    /// <summary>
    /// Formats and writes a log message at the specified log level.
    /// </summary>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="message">Format string of the log message.</param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    public static void Log(LogLevel logLevel, string? message, params object?[] args)
    {
        _logger.Log(logLevel, message, args);
    }

    /// <summary>
    /// Formats and writes a log message at the specified log level.
    /// </summary>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message.</param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    public static void Log(LogLevel logLevel, EventId eventId, string? message, params object?[] args)
    {
        _logger.Log(logLevel, eventId, null, message, args);
    }

    /// <summary>
    /// Formats and writes a log message at the specified log level.
    /// </summary>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message.</param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    public static void Log(LogLevel logLevel, Exception? exception, string? message, params object?[] args)
    {
        _logger.Log(logLevel, 0, exception, message, args);
    }

    /// <summary>Formats and writes a debug log message.</summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogDebug(0, exception, "Error while processing request from {Address}", address)</example>
    public static void LogDebug(
        EventId eventId,
        Exception? exception,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Debug, eventId, exception, message, args);
    }

    /// <summary>Formats and writes a debug log message.</summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogDebug(0, "Processing request from {Address}", address)</example>
    public static void LogDebug(
        EventId eventId,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Debug, eventId, message, args);
    }

    /// <summary>Formats and writes a debug log message.</summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogDebug(exception, "Error while processing request from {Address}", address)</example>
    public static void LogDebug(
        Exception? exception,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Debug, exception, message, args);
    }

    /// <summary>Formats and writes a debug log message.</summary>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogDebug("Processing request from {Address}", address)</example>
    public static void LogDebug(string? message, params object?[] args) => _logger.Log(LogLevel.Debug, message, args);

    /// <summary>Formats and writes a trace log message.</summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogTrace(0, exception, "Error while processing request from {Address}", address)</example>
    public static void LogTrace(
        EventId eventId,
        Exception? exception,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Trace, eventId, exception, message, args);
    }

    /// <summary>Formats and writes a trace log message.</summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogTrace(0, "Processing request from {Address}", address)</example>
    public static void LogTrace(
        EventId eventId,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Trace, eventId, message, args);
    }

    /// <summary>Formats and writes a trace log message.</summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogTrace(exception, "Error while processing request from {Address}", address)</example>
    public static void LogTrace(
        Exception? exception,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Trace, exception, message, args);
    }

    /// <summary>Formats and writes a trace log message.</summary>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogTrace("Processing request from {Address}", address)</example>
    public static void LogTrace(string? message, params object?[] args) => _logger.Log(LogLevel.Trace, message, args);

    /// <summary>Formats and writes an informational log message.</summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogInformation(0, exception, "Error while processing request from {Address}", address)</example>
    public static void LogInformation(
        EventId eventId,
        Exception? exception,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Information, eventId, exception, message, args);
    }

    /// <summary>Formats and writes an informational log message.</summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogInformation(0, "Processing request from {Address}", address)</example>
    public static void LogInformation(
        EventId eventId,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Information, eventId, message, args);
    }

    /// <summary>Formats and writes an informational log message.</summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogInformation(exception, "Error while processing request from {Address}", address)</example>
    public static void LogInformation(
        Exception? exception,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Information, exception, message, args);
    }

    /// <summary>Formats and writes an informational log message.</summary>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogInformation("Processing request from {Address}", address)</example>
    public static void LogInformation(string? message, params object?[] args) =>
        _logger.Log(LogLevel.Information, message, args);

    /// <summary>Formats and writes a warning log message.</summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogWarning(0, exception, "Error while processing request from {Address}", address)</example>
    public static void LogWarning(
        EventId eventId,
        Exception? exception,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Warning, eventId, exception, message, args);
    }

    /// <summary>Formats and writes a warning log message.</summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogWarning(0, "Processing request from {Address}", address)</example>
    public static void LogWarning(
        EventId eventId,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Warning, eventId, message, args);
    }

    /// <summary>Formats and writes a warning log message.</summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogWarning(exception, "Error while processing request from {Address}", address)</example>
    public static void LogWarning(
        Exception? exception,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Warning, exception, message, args);
    }

    /// <summary>Formats and writes a warning log message.</summary>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogWarning("Processing request from {Address}", address)</example>
    public static void LogWarning(string? message, params object?[] args) =>
        _logger.Log(LogLevel.Warning, message, args);

    /// <summary>Formats and writes an error log message.</summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogError(0, exception, "Error while processing request from {Address}", address)</example>
    public static void LogError(
        EventId eventId,
        Exception? exception,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Error, eventId, exception, message, args);
    }

    /// <summary>Formats and writes an error log message.</summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogError(0, "Processing request from {Address}", address)</example>
    public static void LogError(
        EventId eventId,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Error, eventId, message, args);
    }

    /// <summary>Formats and writes an error log message.</summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogError(exception, "Error while processing request from {Address}", address)</example>
    public static void LogError(
        Exception? exception,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Error, exception, message, args);
    }

    /// <summary>Formats and writes an error log message.</summary>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogError("Processing request from {Address}", address)</example>
    public static void LogError(string? message, params object?[] args) => _logger.Log(LogLevel.Error, message, args);

    /// <summary>Formats and writes a critical log message.</summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogCritical(0, exception, "Error while processing request from {Address}", address)</example>
    public static void LogCritical(
        EventId eventId,
        Exception? exception,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Critical, eventId, exception, message, args);
    }

    /// <summary>Formats and writes a critical log message.</summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogCritical(0, "Processing request from {Address}", address)</example>
    public static void LogCritical(
        EventId eventId,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Critical, eventId, message, args);
    }

    /// <summary>Formats and writes a critical log message.</summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogCritical(exception, "Error while processing request from {Address}", address)</example>
    public static void LogCritical(
        Exception? exception,
        string? message,
        params object?[] args)
    {
        _logger.Log(LogLevel.Critical, exception, message, args);
    }

    /// <summary>Formats and writes a critical log message.</summary>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.LogCritical("Processing request from {Address}", address)</example>
    public static void LogCritical(string? message, params object?[] args) =>
        _logger.Log(LogLevel.Critical, message, args);
}