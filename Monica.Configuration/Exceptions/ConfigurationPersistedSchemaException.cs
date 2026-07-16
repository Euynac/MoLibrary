using Monica.Configuration.Models;

namespace Monica.Configuration.Exceptions;

/// <summary>
/// Represents an invalid or incompatible persisted configuration definition schema.
/// </summary>
public sealed class ConfigurationPersistedSchemaException : InvalidOperationException
{
    /// <summary>
    /// Initializes a persisted schema exception.
    /// </summary>
    /// <param name="kind">The stable metadata problem category.</param>
    /// <param name="message">The technical diagnostic message.</param>
    /// <param name="schemaPath">The affected canonical schema path, when known.</param>
    public ConfigurationPersistedSchemaException(
        ConfigurationDefinitionMetadataIssueKind kind,
        string message,
        string? schemaPath = null)
        : base(message)
    {
        Kind = kind;
        SchemaPath = schemaPath;
    }

    /// <summary>
    /// Gets the stable metadata problem category.
    /// </summary>
    public ConfigurationDefinitionMetadataIssueKind Kind { get; }

    /// <summary>
    /// Gets the affected canonical schema path, when known.
    /// </summary>
    public string? SchemaPath { get; }
}
