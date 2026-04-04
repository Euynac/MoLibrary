using Microsoft.EntityFrameworkCore;
using Monica.DependencyInjection.Abstractions;
using Monica.Repository.UnitOfWork.Abstractions;
using Monica.Repository.UnitOfWork.Models;

namespace Monica.Repository.UnitOfWork.Services;

internal class ChildUnitOfWork : IUnitOfWork
{
    public Guid Id => _parent.Id;

    public UnitOfWorkOptions Options => _parent.Options;
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

    public IUnitOfWork? Outer => _parent.Outer;

    public bool IsDisposed => _parent.IsDisposed;
    public Dictionary<string, object?> Items => _parent.Items;

    public bool IsCompleted => _parent.IsCompleted;

    public ICachedServiceProvider CachedServiceProvider => _parent.CachedServiceProvider;

    private readonly IUnitOfWork _parent;

    public ChildUnitOfWork(IUnitOfWork parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        _parent = parent;
    }

    public void SetOuter(IUnitOfWork? outer)
    {
        _parent.SetOuter(outer);
    }

    public void Initialize(UnitOfWorkOptions options)
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
        return $"[Child UnitOfWork of {Id}]";
    }
}
