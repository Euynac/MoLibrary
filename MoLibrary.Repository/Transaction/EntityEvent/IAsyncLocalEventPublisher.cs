namespace MoLibrary.Repository.Transaction.EntityEvent;

/// <summary>
/// Used to trigger entity change events.
/// </summary>
public interface IAsyncLocalEventPublisher
{
    void AddEntityCreatedEvent(object entity);

    void AddEntityUpdatedEvent(object entity);

    void AddEntityDeletedEvent(object entity);

    /// <summary>
    /// bulk publish events in async local buffer
    /// </summary>
    /// <returns></returns>
    Task FlushEventBuffer();
}


public class NullAsyncLocalEventPublisher : IAsyncLocalEventPublisher
{
    public void AddEntityCreatedEvent(object entity)
    {
        return;
    }

    public void AddEntityUpdatedEvent(object entity)
    {
        return;
    }

    public void AddEntityDeletedEvent(object entity)
    {
        return;
    }

    public Task FlushEventBuffer()
    {
        return Task.CompletedTask;
    }
}
