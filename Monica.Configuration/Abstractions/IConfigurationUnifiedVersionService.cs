using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Provides unified configuration version query, comparison, preview, and rollback operations.
/// </summary>
public interface IConfigurationUnifiedVersionService
{
    /// <summary>
    /// Lists unified configuration versions.
    /// </summary>
    /// <param name="from">Earliest creation time to include, or null for no lower bound.</param>
    /// <param name="to">Latest creation time to include, or null for no upper bound.</param>
    /// <param name="definitionKey">Optional definition key filter.</param>
    /// <param name="limit">Maximum number of newest versions to return. Implementations may clamp unsafe values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching version summaries ordered from newest to oldest.</returns>
    Task<IReadOnlyList<ConfigurationUnifiedVersionSummary>> ListVersionsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets one unified configuration version snapshot.
    /// </summary>
    /// <param name="version">The unified version number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The snapshot, or null when the version does not exist.</returns>
    Task<ConfigurationUnifiedVersionSnapshot?> GetVersionAsync(long version, CancellationToken cancellationToken);

    /// <summary>
    /// Compares two unified configuration versions.
    /// </summary>
    /// <param name="originVersion">The baseline version number.</param>
    /// <param name="targetVersion">The target version number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The per-definition comparison between the two versions.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when either version does not exist.</exception>
    Task<ConfigurationUnifiedVersionComparison> CompareVersionsAsync(
        long originVersion,
        long targetVersion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Builds an apply preview for rolling current configuration back to a unified version.
    /// </summary>
    /// <param name="version">The unified version number to preview.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Per-definition target resolution and block diagnostics.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the version does not exist.</exception>
    Task<ConfigurationUnifiedVersionApplyPreview> PreviewRollbackAsync(
        long version,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies the changed, current-schema-compatible definition values from a reviewed unified-version preview.
    /// </summary>
    /// <param name="request">The reviewed rollback request and its preview fingerprint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rollback mutation group and per-definition mutation results.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the preview is stale, a changed definition is blocked, or compatible schema drift was not acknowledged.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when the version does not exist.</exception>
    Task<ConfigurationUnifiedVersionRollbackResult> RollbackToVersionAsync(
        ConfigurationUnifiedVersionRollbackRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Permanently deletes a historical unified configuration version.
    /// </summary>
    /// <remarks>
    /// The latest unified version represents the current version and cannot be deleted. Deleting a historical version
    /// does not renumber the remaining versions, and its version number will not be reused.
    /// </remarks>
    /// <param name="version">The historical unified version number to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the version and its definition documents have been deleted.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the version does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="version"/> is the latest version.</exception>
    Task DeleteVersionAsync(long version, CancellationToken cancellationToken);
}
