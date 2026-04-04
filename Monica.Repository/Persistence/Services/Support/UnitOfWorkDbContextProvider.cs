using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.UnitOfWork.Abstractions;

namespace Monica.Repository.Persistence.Services.Support;

/// <summary>
/// Provides DbContext instances that are bound to the current unit of work.
/// When the current unit of work is transactional, this provider must ensure the DbContext
/// actually enters a database transaction. It must never silently downgrade to a non-transactional
/// DbContext, otherwise repository methods using <c>autoSave: true</c> may commit data before
/// <see cref="IUnitOfWork.CompleteAsync(CancellationToken)"/> is reached.
/// </summary>
public class UnitOfWorkDbContextProvider<TDbContext>(
    IUnitOfWorkManager unitOfWorkManager,
    ILogger<UnitOfWorkDbContextProvider<TDbContext>> logger)
    : IDbContextProvider<TDbContext>
    where TDbContext : DbContext
{
    private static string BuildTransactionRequiredMessage(IUnitOfWork unitOfWork)
    {
        return
            $"Unit of work '{unitOfWork.Id}' requested a database transaction for DbContext '{typeof(TDbContext).FullName}', " +
            "but no active transaction could be started. Continuing without a transaction would allow repository autoSave " +
            "operations to commit data before UnitOfWork.CompleteAsync succeeds.";
    }

    public virtual async Task<TDbContext> GetDbContextAsync()
    {
        var unitOfWork = unitOfWorkManager.Current;
        if (unitOfWork == null)
        {
            throw new Exception("A DbContext can only be created inside a unit of work!");
        }

        var context = unitOfWork.TryGetDbContext<TDbContext>();

        if (context != null)
        {
            return context;
        }
        
        var dbContext = await CreateDbContextAsync(unitOfWork);
        unitOfWork.AttachDbContext(dbContext);
        return dbContext;
    }



    protected virtual async Task<TDbContext> CreateDbContextAsync(IUnitOfWork unitOfWork)
    {
        var dbContext = unitOfWork.Options.IsTransactional
            ? await CreateDbContextWithTransactionAsync(unitOfWork)
            : unitOfWork.CachedServiceProvider.GetRequiredService<TDbContext>();
        if (dbContext is IUnitOfWorkAwareDbContext moDbContext)
        {
            moDbContext.Initialize(unitOfWork);
        }

        return dbContext;
    }


    protected virtual async Task<TDbContext> CreateDbContextWithTransactionAsync(IUnitOfWork unitOfWork, CancellationToken token = default)
    {
        var dbContext = unitOfWork.CachedServiceProvider.GetRequiredService<TDbContext>();

        try
        {
            var dbTransaction = unitOfWork.Options.IsolationLevel.HasValue
                ? await dbContext.Database.BeginTransactionAsync(unitOfWork.Options.IsolationLevel.Value, token)
                : await dbContext.Database.BeginTransactionAsync(token);

            unitOfWork.OnDisposed(() =>
            {
                dbTransaction.Dispose();
            });
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException)
        {
            var message = BuildTransactionRequiredMessage(unitOfWork);
            logger.LogError(e, "{Message}", message);
            throw new InvalidOperationException(message, e);
        }

        if (dbContext.Database.CurrentTransaction == null)
        {
            var message = BuildTransactionRequiredMessage(unitOfWork);
            logger.LogError("{Message}", message);
            throw new InvalidOperationException(message);
        }

        return dbContext;
    }
}
