using Microsoft.EntityFrameworkCore;
using Monica.DependencyInjection.Abstractions;
using Monica.Repository.UnitOfWork.Models;

namespace Monica.Repository.UnitOfWork.Abstractions;

public interface IUnitOfWork : IDisposable
{
    Guid Id { get; }
    ICachedServiceProvider CachedServiceProvider { get; }
    public bool IsDisposed { get; }

    Dictionary<string, object?> Items { get; }
    public bool IsCompleted { get; }
    public IUnitOfWork? Outer { get; }
    void Initialize(UnitOfWorkOptions options);

    /// <summary>
    /// If transactions are enabled, this method must be called to commit the transaction.
    /// If autoSave is not used, calling this method will automatically invoke SaveChanges.
    /// After Complete, the UnitOfWork can no longer obtain a new DbContext.
    /// See DbContextProvider.GetCurrentByChecking for details.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CompleteAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    UnitOfWorkOptions Options { get; }
    void AttachDbContext<TDbContext>(TDbContext dbContext)
        where TDbContext : DbContext;

    TDbContext? TryGetDbContext<TDbContext>()
        where TDbContext : DbContext;


    void OnDisposed(Action handler);
    void OnCompleted(Func<Task> handler);
    void SetOuter(IUnitOfWork? outer);
}
