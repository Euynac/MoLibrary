using Monica.Configuration.Models;

namespace Monica.Configuration.Exceptions;

/// <summary>
/// Indicates that a published configuration definition exists but its persisted metadata is unusable.
/// </summary>
public sealed class ConfigurationDefinitionMetadataUnavailableException : InvalidOperationException
{
    /// <summary>
    /// Initializes the exception from a structured metadata diagnostic.
    /// </summary>
    /// <param name="definitionKey">The affected definition key.</param>
    /// <param name="diagnostic">The persisted metadata diagnostic.</param>
    public ConfigurationDefinitionMetadataUnavailableException(
        string definitionKey,
        ConfigurationDefinitionMetadataDiagnostic diagnostic)
        : base($"Published configuration definition '{definitionKey}' is unavailable: {diagnostic.Message}")
    {
        DefinitionKey = definitionKey;
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Gets the affected definition key.
    /// </summary>
    public string DefinitionKey { get; }

    /// <summary>
    /// Gets the structured metadata diagnostic.
    /// </summary>
    public ConfigurationDefinitionMetadataDiagnostic Diagnostic { get; }
}
