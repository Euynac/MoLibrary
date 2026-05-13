namespace Monica.Configuration.Exceptions;

/// <summary>
/// Thrown when a mutation targets a stale or incompatible schema version.
/// </summary>
public sealed class ConfigurationSchemaMismatchException(string message) : InvalidOperationException(message);
