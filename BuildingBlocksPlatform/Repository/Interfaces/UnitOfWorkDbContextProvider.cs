using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;
using BuildingBlocksPlatform.Transaction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildingBlocksPlatform.Repository.Interfaces;

public class UnitOfWorkDbContextProvider<TDbContext>(
    IMoUnitOfWorkManager unitOfWorkManager)
    : IDbContextProvider<TDbContext>, ITransientDependency
    where TDbContext : DbContext
{
    private const string TransactionsNotSupportedWarningMessage = "Current database does not support transactions. Your database may remain in an inconsistent state in an error case.";

    public ILogger<UnitOfWorkDbContextProvider<TDbContext>> Logger { get; set; } = NullLogger<UnitOfWorkDbContextProvider<TDbContext>>.Instance;

    protected readonly IMoUnitOfWorkManager UnitOfWorkManager = unitOfWorkManager;

    public virtual async Task<TDbContext> GetDbContextAsync()
    {
        var unitOfWork = UnitOfWorkManager.Current;
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


  
    protected virtual async Task<TDbContext> CreateDbContextAsync(IMoUnitOfWork unitOfWork)
    {
        var dbContext = unitOfWork.Options.IsTransactional
            ? await CreateDbContextWithTransactionAsync(unitOfWork)
            : unitOfWork.ServiceProvider.GetRequiredService<TDbContext>();
        if (dbContext is IMoDbContext moDbContext)
        {
            moDbContext.Initialize(unitOfWork);
        }

        return dbContext;
    }

  
    protected virtual async Task<TDbContext> CreateDbContextWithTransactionAsync(IMoUnitOfWork unitOfWork, CancellationToken token = default)
    {
        var dbContext = unitOfWork.ServiceProvider.GetRequiredService<TDbContext>();

        try
        {
            var dbTransaction = unitOfWork.Options.IsolationLevel.HasValue
                ? await dbContext.Database.BeginTransactionAsync(unitOfWork.Options.IsolationLevel.Value, token)
                : await dbContext.Database.BeginTransactionAsync(token);
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException)
        {
            Logger.LogWarning(TransactionsNotSupportedWarningMessage);

            return dbContext;
        }

        return dbContext;
    }
}
