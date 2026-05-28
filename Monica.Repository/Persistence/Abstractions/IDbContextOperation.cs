using Microsoft.EntityFrameworkCore;

namespace Monica.Repository.Persistence.Abstractions;

/// <summary>
/// Executes EF Core work inside an owned dependency injection scope.
/// </summary>
/// <typeparam name="TDbContext">The repository DbContext type used by the operation.</typeparam>
/// <remarks>
/// Use this abstraction from singleton services, hosted services, and other long-lived infrastructure that needs
/// database access without capturing scoped repositories or DbContext instances. Each call creates a scope, resolves
/// a fresh <typeparamref name="TDbContext"/>, runs the supplied delegate, and disposes the scope afterward.
/// </remarks>
public interface IDbContextOperation<TDbContext>
    where TDbContext : DbContext
{
    /// <summary>
    /// Executes an operation that does not return a value.
    /// </summary>
    /// <param name="operation">The operation to run with the scoped DbContext.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    Task ExecuteAsync(
        Func<TDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation that returns a value.
    /// </summary>
    /// <typeparam name="TResult">The result type returned by the operation.</typeparam>
    /// <param name="operation">The operation to run with the scoped DbContext.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The operation result.</returns>
    Task<TResult> ExecuteAsync<TResult>(
        Func<TDbContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
