using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monica.Repository.Persistence.Abstractions;

namespace Monica.Repository.Persistence.Services.Support;

/// <summary>
/// Default scoped operation boundary for repository DbContext access from long-lived services.
/// </summary>
/// <typeparam name="TDbContext">The repository DbContext type used by the operation.</typeparam>
public sealed class ScopedDbContextOperation<TDbContext>(IServiceScopeFactory scopeFactory)
    : IDbContextOperation<TDbContext>
    where TDbContext : DbContext
{
    /// <inheritdoc />
    public async Task ExecuteAsync(
        Func<TDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await operation(dbContext, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<TDbContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await operation(dbContext, cancellationToken);
    }
}
