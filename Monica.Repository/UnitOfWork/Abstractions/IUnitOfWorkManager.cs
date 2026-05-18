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
    Task RunAsync(
        Func<Task> work,
        UnitOfWorkScopeOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs work inside a unit-of-work scope, completes it on success, and returns the result.
    /// </summary>
    Task<T> RunAsync<T>(
        Func<Task<T>> work,
        UnitOfWorkScopeOptions? options = null,
        CancellationToken cancellationToken = default);
}
