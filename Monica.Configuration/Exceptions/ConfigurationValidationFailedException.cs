namespace Monica.Configuration.Exceptions;

/// <summary>
/// Thrown when a configuration value fails validation.
/// </summary>
public sealed class ConfigurationValidationFailedException(string message) : InvalidOperationException(message);
