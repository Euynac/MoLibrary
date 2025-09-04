using Microsoft.EntityFrameworkCore;
using MoLibrary.DependencyInjection.AppInterfaces;

namespace MoLibrary.Repository.Transaction;

public interface IMoUnitOfWork : IDisposable, IMoServiceProviderAccessor
{
    Guid Id { get; }
    public bool IsDisposed { get; }

    Dictionary<string, object?> Items { get; }
    public bool IsCompleted { get; }
    public IMoUnitOfWork? Outer { get; }
    void Initialize(MoUnitOfWorkOptions options);

    /// <summary>
    /// 如果开启了事务，必须调用这个方法才会提交事务。如果没有使用autoSave，调用此方法会自动SaveChanges。
    /// Complete 后 UnitOfWork 无法再次获取新的 DbContext，详见DbContextProvider中 GetCurrentByChecking
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
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
