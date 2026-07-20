using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Monica.Repository.Persistence.Services.Support;

/// <summary>
/// Creates repository DbContexts inside independent service scopes and transfers ownership of each scope to its
/// context. This keeps factory-created contexts safe when the factory is injected into a singleton or another
/// long-lived service.
/// </summary>
/// <typeparam name="TDbContext">The repository DbContext type to create.</typeparam>
internal sealed class OwnedScopeDbContextFactory<TDbContext>(IServiceScopeFactory scopeFactory)
    : IDbContextFactory<TDbContext>
    where TDbContext : RepositoryDbContext<TDbContext>
{
    /// <inheritdoc />
    public TDbContext CreateDbContext()
    {
        return CreateContext(scopeFactory.CreateScope());
    }

    /// <inheritdoc />
    public async ValueTask<TDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var scope = scopeFactory.CreateAsyncScope();
        try
        {
            var context = ActivatorUtilities.CreateInstance<TDbContext>(scope.ServiceProvider);
            context.OwnFactoryScope(scope);
            return context;
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static TDbContext CreateContext(IServiceScope scope)
    {
        try
        {
            var context = ActivatorUtilities.CreateInstance<TDbContext>(scope.ServiceProvider);
            context.OwnFactoryScope(scope);
            return context;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}
