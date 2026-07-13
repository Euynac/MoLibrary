namespace Monica.Configuration.Exceptions;

/// <summary>
/// Thrown when a configuration mutation fails optimistic concurrency checks.
/// </summary>
public sealed class ConfigurationConcurrencyConflictException : InvalidOperationException
{
    /// <summary>
    /// Creates a concurrency conflict with a diagnostic message.
    /// </summary>
    /// <param name="message">Conflict diagnostic.</param>
    public ConfigurationConcurrencyConflictException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a concurrency conflict caused by a persistence-provider concurrency exception.
    /// </summary>
    /// <param name="message">Conflict diagnostic.</param>
    /// <param name="innerException">Underlying provider exception.</param>
    public ConfigurationConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
