namespace Monica.Configuration.Exceptions;

/// <summary>
/// Thrown when a configuration definition cannot be found.
/// </summary>
public sealed class ConfigurationDefinitionNotFoundException(string definitionKey)
    : KeyNotFoundException($"Configuration definition '{definitionKey}' was not found.");
