using Monica.Repository.UnitOfWork.Abstractions;

namespace Monica.Repository.UnitOfWork.Services;

internal sealed class ChildUnitOfWork(IUnitOfWork parent) : IUnitOfWork
{
    public Guid Id => parent.Id;

    public bool IsCompleted => parent.IsCompleted;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return parent.SaveChangesAsync(cancellationToken);
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        return parent.RollbackAsync(cancellationToken);
    }

    public void OnCompleted(Func<Task> handler)
    {
        parent.OnCompleted(handler);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
    }

    public override string ToString()
    {
        return $"[Child UnitOfWork of {Id}]";
    }
}
