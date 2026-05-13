namespace Monica.Configuration.Exceptions;

/// <summary>
/// Thrown when a configuration mutation fails optimistic concurrency checks.
/// </summary>
public sealed class ConfigurationConcurrencyConflictException(string message) : InvalidOperationException(message);
