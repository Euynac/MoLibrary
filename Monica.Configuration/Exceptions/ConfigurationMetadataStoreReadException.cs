using Monica.Configuration.Models;

namespace Monica.Configuration.Exceptions;

/// <summary>
/// Represents a classified store-wide failure while reading published configuration metadata.
/// </summary>
public sealed class ConfigurationMetadataStoreReadException : InvalidOperationException
{
    /// <summary>
    /// Initializes a classified metadata-store read failure.
    /// </summary>
    /// <param name="kind">The stable store failure category.</param>
    /// <param name="message">The operator-facing technical message.</param>
    /// <param name="innerException">The provider exception that caused the read failure, when one exists.</param>
    public ConfigurationMetadataStoreReadException(
        ConfigurationMetadataStoreIssueKind kind,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>
    /// Gets the stable metadata-store failure category.
    /// </summary>
    public ConfigurationMetadataStoreIssueKind Kind { get; }
}
