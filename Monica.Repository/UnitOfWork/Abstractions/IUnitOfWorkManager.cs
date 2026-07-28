using Monica.Repository.UnitOfWork.Models;

namespace Monica.Repository.UnitOfWork.Abstractions;

/// <summary>
/// Manages ambient unit-of-work scopes.
/// </summary>
public interface IUnitOfWorkManager
{
    /// <summary>
    /// Gets the current active unit of work, or <see langword="null"/> when no scope is active.
    /// </summary>
    IUnitOfWork? Current { get; }

    /// <summary>
    /// Begins a unit-of-work scope.
    /// </summary>
    /// <param name="options">Scope options. When omitted, a transactional scope is created.</param>
    /// <returns>The opened unit-of-work scope.</returns>
    IUnitOfWork BeginScope(UnitOfWorkScopeOptions? options = null);

    /// <summary>
    /// Runs work inside a unit-of-work scope and completes it on success.
    /// </summary>
    /// <param name="work">The asynchronous operation executed inside the ambient scope.</param>
    /// <param name="options">Optional scope settings. The operation joins an ambient scope unless a new scope is required.</param>
    /// <param name="cancellationToken">The token used while completing the successful scope.</param>
    /// <returns>A task representing the complete operation and unit-of-work completion.</returns>
    /// <remarks>
    /// Operation and completion failures are propagated without changing their concrete exception type or stack.
    /// Rollback ignores an already-canceled operation token. If rollback also fails, that failure is attached to the
    /// primary exception under <c>Monica.Repository.UnitOfWork.RollbackException</c> in <see cref="Exception.Data"/>.
    /// </remarks>
    Task RunAsync(
        Func<Task> work,
        UnitOfWorkScopeOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs work inside a unit-of-work scope, completes it on success, and returns the result.
    /// </summary>
    /// <typeparam name="T">The operation result type.</typeparam>
    /// <param name="work">The asynchronous operation executed inside the ambient scope.</param>
    /// <param name="options">Optional scope settings. The operation joins an ambient scope unless a new scope is required.</param>
    /// <param name="cancellationToken">The token used while completing the successful scope.</param>
    /// <returns>The operation result after the unit of work completes successfully.</returns>
    /// <remarks>
    /// Operation and completion failures are propagated without changing their concrete exception type or stack.
    /// Rollback ignores an already-canceled operation token. If rollback also fails, that failure is attached to the
    /// primary exception under <c>Monica.Repository.UnitOfWork.RollbackException</c> in <see cref="Exception.Data"/>.
    /// </remarks>
    Task<T> RunAsync<T>(
        Func<Task<T>> work,
        UnitOfWorkScopeOptions? options = null,
        CancellationToken cancellationToken = default);
}
