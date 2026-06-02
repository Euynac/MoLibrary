namespace Monica.Repository.UnitOfWork.Abstractions;

/// <summary>
/// Represents an application-level persistence boundary.
/// </summary>
/// <remarks>
/// A unit of work coordinates SaveChanges, transactions, and completion callbacks for participating DbContexts.
/// Dispose the scope asynchronously when possible; an incomplete scope rolls back explicitly during disposal.
/// </remarks>
public interface IUnitOfWork : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the unique identifier of this unit-of-work scope.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets whether the scope has completed successfully.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Saves changes for all DbContexts attached to this unit of work without committing the transaction.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves changes, publishes buffered events, commits transactions, and runs completion handlers.
    /// </summary>
    Task CompleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back all active transactions attached to this unit of work.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a handler that runs after commit succeeds.
    /// </summary>
    void OnCompleted(Func<Task> handler);
}
