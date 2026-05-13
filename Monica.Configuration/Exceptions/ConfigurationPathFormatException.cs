namespace Monica.Configuration.Exceptions;

/// <summary>
/// Thrown when a logical configuration path cannot be parsed.
/// </summary>
public sealed class ConfigurationPathFormatException(string message) : FormatException(message);
