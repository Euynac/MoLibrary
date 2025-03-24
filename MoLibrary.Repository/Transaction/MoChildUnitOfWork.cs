using Microsoft.EntityFrameworkCore;
using MoLibrary.Tool.Utils;

namespace MoLibrary.Repository.Transaction;

internal class MoChildUnitOfWork : IMoUnitOfWork
{
    public Guid Id => _parent.Id;

    public MoUnitOfWorkOptions Options => _parent.Options;
    public void AttachDbContext<TDbContext>(TDbContext dbContext) where TDbContext : DbContext
    {
        _parent.AttachDbContext(dbContext);
    }

    public TDbContext? TryGetDbContext<TDbContext>() where TDbContext : DbContext
    {
        return _parent.TryGetDbContext<TDbContext>();
    }

    public void OnDisposed(Action handler)
    {
        _parent.OnDisposed(handler);
    }

    public IMoUnitOfWork? Outer => _parent.Outer;

    public bool IsDisposed => _parent.IsDisposed;
    public Dictionary<string, object> Items => _parent.Items;

    public bool IsCompleted => _parent.IsCompleted;

    public IServiceProvider ServiceProvider => _parent.ServiceProvider;

    private readonly IMoUnitOfWork _parent;

    public MoChildUnitOfWork(IMoUnitOfWork parent)
    {
        Check.NotNull(parent, nameof(parent));
        _parent = parent;
    }

    public void SetOuter(IMoUnitOfWork? outer)
    {
        _parent.SetOuter(outer);
    }

    public void Initialize(MoUnitOfWorkOptions options)
    {
        _parent.Initialize(options);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _parent.SaveChangesAsync(cancellationToken);
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        return _parent.RollbackAsync(cancellationToken);
    }

    public void OnCompleted(Func<Task> handler)
    {
        _parent.OnCompleted(handler);
    }

    public void Dispose()
    {

    }

    public override string ToString()
    {
        return $"[Child MoUnitOfWork of {Id}]";
    }
}
