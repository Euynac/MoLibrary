using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Writes supported physical JSON configuration sources.
/// </summary>
public interface IConfigurationJsonFileSourceWriter
{
    /// <summary>
    /// Reads selected physical JSON-source values and their shared source-content revision.
    /// </summary>
    /// <param name="source">The readable JSON source descriptor.</param>
    /// <param name="configurationPaths">The distinct Microsoft configuration paths to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The requested physical values and source revision captured under one source lock.</returns>
    Task<ConfigurationJsonFilePhysicalValuesSnapshot> ReadPhysicalValuesAsync(
        ConfigurationSourceDescriptor source,
        IReadOnlyList<string> configurationPaths,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads selected physical JSON-source values together with the revision that contained them.
    /// </summary>
    /// <param name="source">The readable JSON source descriptor.</param>
    /// <param name="definition">The definition used to fingerprint schema-visible source values.</param>
    /// <param name="configurationPaths">The distinct Microsoft configuration paths to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The values and source revision captured under one source lock.</returns>
    Task<ConfigurationJsonFileValuesSnapshot> ReadValuesAsync(
        ConfigurationSourceDescriptor source,
        ConfigurationDefinition definition,
        IReadOnlyList<string> configurationPaths,
        CancellationToken cancellationToken);

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
    /// Applies multiple mutations to one JSON source through one revision check and one atomic file replacement.
    /// </summary>
    /// <param name="source">The source descriptor.</param>
    /// <param name="mutations">Mutations in deterministic application order.</param>
    /// <param name="expectedRevision">Optional expected source revision.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The shared revision and per-mutation value outcomes.</returns>
    Task<ConfigurationJsonFileBatchWriteResult> WriteBatchAsync(
        ConfigurationSourceDescriptor source,
        IReadOnlyList<ConfigurationJsonFileMutation> mutations,
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
