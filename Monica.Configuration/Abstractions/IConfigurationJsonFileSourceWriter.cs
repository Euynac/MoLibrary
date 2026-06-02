using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Writes supported physical JSON configuration sources.
/// </summary>
public interface IConfigurationJsonFileSourceWriter
{
    /// <summary>
    /// Applies a JSON source mutation.
    /// </summary>
    /// <param name="source">The source descriptor.</param>
    /// <param name="configurationPath">The Microsoft configuration path.</param>
    /// <param name="mutationKind">The mutation kind.</param>
    /// <param name="value">The JSON value.</param>
    /// <param name="expectedRevision">Optional expected source revision.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The write result.</returns>
    Task<ConfigurationJsonFileWriteResult> WriteAsync(
        ConfigurationSourceDescriptor source,
        string configurationPath,
        ConfigurationMutationKind mutationKind,
        ConfigurationStoredValue value,
        string? expectedRevision,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current source revision hash.
    /// </summary>
    /// <param name="source">The source descriptor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The revision hash, or null when unavailable.</returns>
    Task<string?> GetRevisionAsync(ConfigurationSourceDescriptor source, CancellationToken cancellationToken);
}
