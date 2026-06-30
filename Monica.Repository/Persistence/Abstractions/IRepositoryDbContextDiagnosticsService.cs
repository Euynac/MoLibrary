using Monica.Repository.Persistence.Models;

namespace Monica.Repository.Persistence.Abstractions;

/// <summary>
/// Reads runtime diagnostics and executes migration operations for registered Repository DbContexts.
/// </summary>
public interface IRepositoryDbContextDiagnosticsService
{
    /// <summary>
    /// Builds runtime snapshots for all registered Repository DbContexts.
    /// </summary>
    /// <param name="revealConnectionStrings">Whether to include full connection strings in the result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Runtime snapshots for all registered contexts.</returns>
    Task<IReadOnlyList<RepositoryDbContextSnapshot>> GetRegisteredContextsAsync(
        bool revealConnectionStrings = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads activity rows for one registered Repository DbContext.
    /// </summary>
    /// <param name="contextId">The selected DbContext identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Activity query result.</returns>
    Task<RepositoryActivityResult> GetActivityAsync(
        string contextId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads migration status for one registered Repository DbContext.
    /// </summary>
    /// <param name="contextId">The selected DbContext identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Migration status.</returns>
    Task<RepositoryMigrationStatus> GetMigrationStatusAsync(
        string contextId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies pending migrations for one registered Repository DbContext.
    /// </summary>
    /// <param name="contextId">The selected DbContext identifier.</param>
    /// <param name="commandTimeout">The command timeout to apply while migration runs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The migration update result.</returns>
    Task<RepositoryMigrationUpdateResult> UpdateMigrationAsync(
        string contextId,
        TimeSpan commandTimeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies pending migrations for every registered Repository DbContext that has pending migrations.
    /// </summary>
    /// <param name="commandTimeout">The command timeout to apply while each migration runs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Per-context migration update results.</returns>
    Task<IReadOnlyList<RepositoryMigrationUpdateResult>> UpdateAllPendingMigrationsAsync(
        TimeSpan commandTimeout,
        CancellationToken cancellationToken = default);
}
