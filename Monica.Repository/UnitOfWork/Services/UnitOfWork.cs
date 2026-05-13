using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.DependencyInjection.Abstractions;
using Monica.Repository.UnitOfWork.Abstractions;
using Monica.Repository.UnitOfWork.Models;

namespace Monica.Repository.UnitOfWork.Services;

/// <summary>
/// Default unit-of-work implementation.
/// </summary>
internal sealed class UnitOfWork(
    UnitOfWorkScopeOptions options,
    ICachedServiceProvider serviceProvider,
    IAsyncLocalEventPublisher publisher,
    ILogger<UnitOfWork> logger)
    : IUnitOfWork, IUnitOfWorkInternals
{
    private readonly Dictionary<string, DbContext> _dbContexts = [];
    private readonly List<Func<Task>> _completedHandlers = [];
    private readonly List<Action> _disposedHandlers = [];
    private Exception? _exception;
    private bool _isCompleting;
    private bool _isRolledBack;
    private AsyncEventBuffer? _eventBuffer;

    public Guid Id { get; } = Guid.NewGuid();

    public UnitOfWorkScopeOptions Options { get; } = options;

    public IUnitOfWork? Outer { get; private set; }

    public bool IsDisposed { get; private set; }

    public bool IsCompleted { get; private set; }

    public ICachedServiceProvider CachedServiceProvider { get; } = serviceProvider;

    public void SetOuter(IUnitOfWork? outer)
    {
        Outer = outer;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_isRolledBack)
        {
            return;
        }

        foreach (var dbContext in _dbContexts.Values)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task CompleteAsync(CancellationToken cancellationToken = default)
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
        catch (Exception exception)
        {
            _exception = exception;
            try
            {
                await RollbackAllAsync(cancellationToken);
            }
            catch (Exception rollbackException)
            {
                logger.LogError(
                    rollbackException,
                    "An error occurred while rolling back the unit of work after a completion failure.");
            }

            exception.ReThrow();
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
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
        _disposedHandlers.Add(handler);
    }

    public void OnCompleted(Func<Task> handler)
    {
        _completedHandlers.Add(handler);
    }

    public void AttachDbContext<TDbContext>(TDbContext dbContext)
        where TDbContext : DbContext
    {
        _dbContexts[typeof(TDbContext).FullName!] = dbContext;
    }

    public TDbContext? TryGetDbContext<TDbContext>()
        where TDbContext : DbContext
    {
        return _dbContexts.TryGetValue(typeof(TDbContext).FullName!, out var dbContext)
            ? (TDbContext)dbContext
            : null;
    }

    public AsyncEventBuffer? GetEventBuffer()
    {
        return _eventBuffer;
    }

    public AsyncEventBuffer GetOrCreateEventBuffer()
    {
        return _eventBuffer ??= new AsyncEventBuffer();
    }

    public async ValueTask DisposeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            if (!IsCompleted || _exception != null)
            {
                await RollbackAsync();
                OnFailed();
            }

            OnDisposed();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exception occurred while disposing {UnitOfWorkType}.", nameof(UnitOfWork));
        }
        finally
        {
            IsDisposed = true;
            DisposeDbContexts();
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public override string ToString()
    {
        return $"[UnitOfWork {Id}]";
    }

    private async Task OnCompletedAsync()
    {
        foreach (var handler in _completedHandlers)
        {
            await handler.Invoke();
        }
    }

    private void OnFailed()
    {
    }

    private void OnDisposed()
    {
        foreach (var handler in _disposedHandlers)
        {
            handler.Invoke();
        }
    }

    private void PreventMultipleComplete()
    {
        if (!IsCompleted && !_isCompleting)
        {
            return;
        }

        if (_exception != null)
        {
            throw new Exception(
                $"Completion has already been requested for this unit of work, but it failed: {_exception.Message}. See inner exception for detail.",
                _exception);
        }

        throw new Exception("Completion has already been requested for this unit of work.");
    }

    private async Task RollbackAllAsync(CancellationToken cancellationToken)
    {
        if (!Options.IsTransactional)
        {
            return;
        }

        foreach (var dbContext in _dbContexts.Values)
        {
            if (dbContext.Database.CurrentTransaction != null)
            {
                await dbContext.Database.RollbackTransactionAsync(cancellationToken);
            }
        }
    }

    private async Task CommitTransactionsAsync(CancellationToken cancellationToken)
    {
        if (!Options.IsTransactional)
        {
            return;
        }

        foreach (var dbContext in _dbContexts.Values)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (dbContext.Database.CurrentTransaction != null)
            {
                await dbContext.Database.CommitTransactionAsync(cancellationToken);
            }
        }
    }

    private void DisposeDbContexts()
    {
        try
        {
            foreach (var dbContext in _dbContexts.Values)
            {
                dbContext.Dispose();
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exception occurred while disposing DbContexts attached to {UnitOfWorkType}.", nameof(UnitOfWork));
        }
    }
}
