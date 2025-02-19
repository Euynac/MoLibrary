using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocksPlatform.Transaction;

public interface IMoUnitOfWork : IDisposable, IMoServiceProviderAccessor
{
    Guid Id { get; }
    public bool IsDisposed { get;  }

    Dictionary<string, object> Items { get; }
    public bool IsCompleted { get; }
    public IMoUnitOfWork? Outer { get; }
    void Initialize(MoUnitOfWorkOptions options);

    Task CompleteAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    MoUnitOfWorkOptions Options { get; }
    void AttachDbContext<TDbContext>(TDbContext dbContext)
        where TDbContext : DbContext;

    TDbContext? TryGetDbContext<TDbContext>()
        where TDbContext : DbContext;


    void OnDisposed(Action handler);
    void OnCompleted(Func<Task> handler);
    void SetOuter(IMoUnitOfWork? outer);
}
