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
    /// Applies the captured definition values from a unified version.
    /// </summary>
    /// <param name="version">The unified version number to apply.</param>
    /// <param name="reason">Optional rollback reason recorded in normal mutation history.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rollback mutation group and per-definition mutation results.</returns>
    /// <exception cref="InvalidOperationException">Thrown when preview target resolution blocks any captured definition.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the version does not exist.</exception>
    Task<ConfigurationUnifiedVersionRollbackResult> RollbackToVersionAsync(
        long version,
        string? reason,
        CancellationToken cancellationToken);
}
