using BuildingBlocksPlatform.Transaction.EntityEvent;
using Microsoft.EntityFrameworkCore;
using MoLibrary.DependencyInjection.AppInterfaces;

namespace BuildingBlocksPlatform.Transaction;

public class MoUnitOfWork(
    IMoServiceProvider serviceProvider,
    IAsyncLocalEventPublisher publisher)
    : IMoUnitOfWork
{
    public Guid Id { get; } = Guid.NewGuid();

    public MoUnitOfWorkOptions Options { get; private set; } = null!;

    public IMoUnitOfWork? Outer { get; private set; }

    public bool IsDisposed { get; private set; }

    public bool IsCompleted { get; private set; }

    protected List<Func<Task>> CompletedHandlers { get; } = [];
    protected List<Action> DisposedHandlers { get; } = [];

    public IServiceProvider ServiceProvider { get; set; } = serviceProvider.ServiceProvider;

    public Dictionary<string, object> Items { get; } = [];

    private readonly Dictionary<string, DbContext> _dbContexts = [];

    private Exception? _exception;
    private bool _isCompleting;
    private bool _isRolledBack;

    public virtual void Initialize(MoUnitOfWorkOptions options)
    {
        if (Options != null)
        {
            throw new Exception("This unit of work has already been initialized.");
        }

        Options = options;
    }
    

    public virtual void SetOuter(IMoUnitOfWork? outer)
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
            ex.ReThrow();
        }
    }

    public virtual async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_isRolledBack)
        {
            return;
        }

        _isRolledBack = true;

        await RollbackAllAsync(cancellationToken);
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
        // TODO 是否应该自动提交事务？
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;

        if (!IsCompleted || _exception != null)
        {
            OnFailed();
        }

        OnDisposed();

        //dispose all db contexts
        foreach (var dbContext in _dbContexts.Values)
        {
            dbContext.Dispose();
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
            ? (TDbContext) dbContext
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