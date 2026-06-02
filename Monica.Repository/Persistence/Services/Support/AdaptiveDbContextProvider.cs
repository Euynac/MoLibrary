using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.UnitOfWork.Abstractions;

namespace Monica.Repository.Persistence.Services.Support;

/// <summary>
/// Provides DbContext instances either from the active unit of work or from the current dependency injection scope.
/// </summary>
/// <typeparam name="TDbContext">The DbContext type.</typeparam>
/// <remarks>
/// Inside a unit of work, DbContexts are attached to the unit-of-work transaction. Outside a unit of work,
/// the scoped DbContext is returned and callers commit explicitly through repository <c>SaveChangesAsync</c>.
/// </remarks>
public class AdaptiveDbContextProvider<TDbContext>(
    IUnitOfWorkManager unitOfWorkManager,
    IServiceProvider serviceProvider,
    ILogger<AdaptiveDbContextProvider<TDbContext>> logger)
    : IDbContextProvider<TDbContext>
    where TDbContext : DbContext
{
    private static string BuildTransactionRequiredMessage(IUnitOfWork unitOfWork)
    {
        return
            $"Unit of work '{unitOfWork.Id}' requested a database transaction for DbContext '{typeof(TDbContext).FullName}', " +
            "but no active transaction could be started. Continuing without a transaction would break unit-of-work commit and rollback semantics.";
    }

    /// <inheritdoc />
    public virtual async Task<TDbContext> GetDbContextAsync()
    {
        if (unitOfWorkManager.Current is not { } unitOfWork)
        {
            return serviceProvider.GetRequiredService<TDbContext>();
        }

        var internals = GetInternals(unitOfWork);
        var context = internals.TryGetDbContext<TDbContext>();

        if (context != null)
        {
            return context;
        }

        var dbContext = await CreateDbContextAsync(unitOfWork, internals);
        internals.AttachDbContext(dbContext);
        return dbContext;
    }

    private async Task<TDbContext> CreateDbContextAsync(IUnitOfWork unitOfWork, IUnitOfWorkInternals internals)
    {
        var dbContext = internals.Options.IsTransactional
            ? await CreateDbContextWithTransactionAsync(unitOfWork, internals)
            : internals.CachedServiceProvider.GetRequiredService<TDbContext>();

        if (dbContext is IUnitOfWorkAwareDbContext moDbContext)
        {
            moDbContext.Initialize(internals.Options);
        }

        return dbContext;
    }

    private async Task<TDbContext> CreateDbContextWithTransactionAsync(
        IUnitOfWork unitOfWork,
        IUnitOfWorkInternals internals,
        CancellationToken cancellationToken = default)
    {
        var dbContext = internals.CachedServiceProvider.GetRequiredService<TDbContext>();

        try
        {
            var dbTransaction = internals.Options.IsolationLevel.HasValue
                ? await dbContext.Database.BeginTransactionAsync(internals.Options.IsolationLevel.Value, cancellationToken)
                : await dbContext.Database.BeginTransactionAsync(cancellationToken);

            internals.OnDisposed(dbTransaction.Dispose);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            var message = BuildTransactionRequiredMessage(unitOfWork);
            logger.LogError(exception, "{Message}", message);
            throw new InvalidOperationException(message, exception);
        }

        if (dbContext.Database.CurrentTransaction == null)
        {
            var message = BuildTransactionRequiredMessage(unitOfWork);
            logger.LogError("{Message}", message);
            throw new InvalidOperationException(message);
        }

        return dbContext;
    }

    private static IUnitOfWorkInternals GetInternals(IUnitOfWork unitOfWork)
    {
        return unitOfWork as IUnitOfWorkInternals
            ?? throw new InvalidOperationException(
                $"The active unit of work '{unitOfWork.GetType().FullName}' does not expose framework internals.");
    }
}
