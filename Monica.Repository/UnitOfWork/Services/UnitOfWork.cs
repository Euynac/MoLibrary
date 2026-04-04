using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.DependencyInjection.Abstractions;
using Monica.Repository.UnitOfWork.Abstractions;
using Monica.Repository.UnitOfWork.Models;

namespace Monica.Repository.UnitOfWork.Services;

public class UnitOfWork(
    ICachedServiceProvider serviceProvider,
    IAsyncLocalEventPublisher publisher,
    ILogger<UnitOfWork> logger)
    : IUnitOfWork
{
    public Guid Id { get; } = Guid.NewGuid();

    public UnitOfWorkOptions Options { get; private set; } = null!;

    public IUnitOfWork? Outer { get; private set; }

    public bool IsDisposed { get; private set; }

    public bool IsCompleted { get; private set; }

    protected List<Func<Task>> CompletedHandlers { get; } = [];
    protected List<Action> DisposedHandlers { get; } = [];

    public ICachedServiceProvider CachedServiceProvider { get; } = serviceProvider;

    public Dictionary<string, object?> Items { get; } = [];

    private readonly Dictionary<string, DbContext> _dbContexts = [];

    private Exception? _exception;
    private bool _isCompleting;
    private bool _isRolledBack;

    public virtual void Initialize(UnitOfWorkOptions options)
    {
        if (Options != null)
        {
            throw new Exception("This unit of work has already been initialized.");
        }

        Options = options;
    }


    public virtual void SetOuter(IUnitOfWork? outer)
    {
        Outer = outer;
    }

    public virtual async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_isRolledBack)
        {
            return;
        }

        foreach (var dbContext in _dbContexts)
        {
            await dbContext.Value.SaveChangesAsync(cancellationToken);
        }
    }


    public virtual async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (_isRolledBack)
        {
            return;
        }

        PreventMultipleComplete();

        try
        {
            _isCompleting = true;
            await SaveChangesAsync(cancellationToken);

            await publisher.FlushEventBuffer();
            await CommitTransactionsAsync(cancellationToken);
            await OnCompletedAsync();
            IsCompleted = true;
            _isCompleting = false;
        }
        catch (Exception ex)
        {
            _exception = ex;
            try
            {
                await RollbackAllAsync(cancellationToken);
            }
            catch (Exception rollbackException)
            {
                logger.LogError(rollbackException, "An error occurred while rolling back the unit of work after a completion failure.");
            }

            ex.ReThrow();
        }
    }

    public virtual async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_isRolledBack)
        {
            return;
        }

        await RollbackAllAsync(cancellationToken);

        _isRolledBack = true;
    }

    public void OnDisposed(Action handler)
    {
        DisposedHandlers.Add(handler);
    }

    public virtual void OnCompleted(Func<Task> handler)
    {
        CompletedHandlers.Add(handler);
    }

    protected virtual async Task OnCompletedAsync()
    {
        foreach (var handler in CompletedHandlers)
        {
            await handler.Invoke();
        }
    }

    protected virtual void OnFailed()
    {
    }

    protected virtual void OnDisposed()
    {
        foreach (var handler in DisposedHandlers)
        {
            handler.Invoke();
        }
    }

    public virtual void Dispose()
    {
        try
        {
            if (IsDisposed) // This can also be triggered once when the scoped ServiceProvider is disposed.
            {
                return;
            }

            if (!IsCompleted || _exception != null)
            {
                OnFailed();
            }

            // In CreateDbContextWithTransactionAsync, disposing an uncommitted transaction usually triggers an automatic rollback.
            // After rollback, the transaction is already finished and should not be committed again.
            // Forcibly terminating the process may also trigger rollback when the DB detects a disconnected client (not always reliable in practice).
            OnDisposed();

            IsDisposed = true;

        }
        catch (Exception e)
        {
            logger.LogError(e, $"Exception occur when {nameof(UnitOfWork)} is disposing!");
        }
        finally
        {
            try
            {
                //dispose all db contexts
                foreach (var dbContext in _dbContexts.Values)
                {
                    dbContext.Dispose(); // Disposing the DbContext usually rolls back uncommitted transactions.
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Exception occur when {nameof(UnitOfWork)} is try disposing all related DbContexts!");
            }
           
        }
    }

    public virtual void AttachDbContext<TDbContext>(TDbContext dbContext)
        where TDbContext : DbContext
    {
        _dbContexts[typeof(TDbContext).FullName!] = dbContext;
    }

    public virtual TDbContext? TryGetDbContext<TDbContext>()
        where TDbContext : DbContext
    {
        return _dbContexts.TryGetValue(typeof(TDbContext).FullName!, out var dbContext)
            ? (TDbContext)dbContext
            : null;
    }

    private void PreventMultipleComplete()
    {
        if (IsCompleted || _isCompleting)
        {
            if (_exception != null)
            {
                throw new Exception($"Completion has already been requested for this unit of work. but has error:{_exception.Message}, see inner exception for detail", _exception);
            }
            throw new Exception("Completion has already been requested for this unit of work. Detect has multiple thread to manipulate this unit of work");
        }
    }

    protected virtual async Task RollbackAllAsync(CancellationToken cancellationToken)
    {
        if (!Options.IsTransactional)
        {
            return;
        }

        foreach (var dbContext in _dbContexts.Values)
        {
            await dbContext.Database.RollbackTransactionAsync(cancellationToken);
        }
    }

    protected virtual async Task CommitTransactionsAsync(CancellationToken cancellationToken)
    {
        if (!Options.IsTransactional)
        {
            return;
        }

        foreach (var dbContext in _dbContexts.Values)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await dbContext.Database.CommitTransactionAsync(cancellationToken);
        }
    }

    public override string ToString()
    {
        return $"[UnitOfWork {Id}]";
    }
}
