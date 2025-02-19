using BuildingBlocksPlatform.DependencyInjection;
using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;
using BuildingBlocksPlatform.EventBus.Abstractions;
using BuildingBlocksPlatform.ObjectMapper;
using BuildingBlocksPlatform.Repository.EntityInterfaces;
using BuildingBlocksPlatform.Repository.Extensions;
using Microsoft.Extensions.Options;




namespace BuildingBlocksPlatform.Transaction.EntityEvent;


public class AsyncEventBuffer
{
    public List<TransactionEventRecord> Records { get; set; } = [];
    public List<TransactionEventRecord> DistributedEvents { get; } = [];
    public List<TransactionEventRecord> LocalEvents { get; } = [];

    public HashSet<int> DistributedEventsHash { get; } = [];
    public HashSet<int> LocalEventsHash { get; } = [];

    public async Task Flush(ILocalEventBus eventBus, IDistributedEventBus distributedEventBus)
    {
        while (LocalEvents.Count != 0 || DistributedEvents.Count != 0)
        {
            if (LocalEvents.Count != 0)
            {
                var group = LocalEvents.GroupBy(p => p.EventType).ToDictionary(p => p.Key, p => p.Select(r => r.EventData).ToList());
                LocalEvents.Clear();
                LocalEventsHash.Clear();
                foreach (var record in group)
                {
                    await eventBus.BulkPublishAsync(record.Key, record.Value);
                }

            }

            if (DistributedEvents.Count != 0)
            {
                var group = DistributedEvents.GroupBy(p => p.EventType).ToDictionary(p => p.Key, p => p.Select(r => r.EventData).ToList());
                DistributedEvents.Clear();
                DistributedEventsHash.Clear();
                foreach (var record in group)
                {
                    await distributedEventBus.BulkPublishAsync(record.Key, record.Value);
                }
            }
        }
    }
}


public class AsyncLocalEventStore(IMoUnitOfWorkManager uow) : IAsyncLocalEventStore
{
    //TODO 测试潜在内存泄漏情况
    //private static readonly AsyncLocal<AsyncEventBuffer> _asyncBuffer = new();
    //由于ABP interceptor会影响AsyncLocal的功能，待分离后使用


    public AsyncEventBuffer? GetBuffer()
    {
        if (uow.Current is not { } u) return null;
        if (u.Items.TryGetValue(nameof(AsyncLocalEventStore), out var buffer) && buffer is AsyncEventBuffer b)
        {
            return b;
        }

        return null;
    }

    public AsyncEventBuffer GetOrNewBuffer()
    {
        using var u = uow.Begin();
        return (u.Items.GetOrAdd(nameof(AsyncLocalEventStore), () => new AsyncEventBuffer()) as AsyncEventBuffer)!;
    }
}

public interface IAsyncLocalEventStore
{
    public AsyncEventBuffer? GetBuffer();
    public AsyncEventBuffer GetOrNewBuffer();
}

/// <summary>
/// Used to trigger entity change events.
/// </summary>
public class AsyncLocalEventPublisher(
    IObjectMapper entityToEtoMapper,
    IOptions<DistributedEntityEventOptions> distributedEntityEventOptions,
    ILocalEventBus localEventBus,
    IDistributedEventBus distributedEventBus,
    ILogger<AsyncLocalEventPublisher> logger, IAsyncLocalEventStore bufferStore) : IAsyncLocalEventPublisher
{
    public ILocalEventBus LocalEventBus { get; set; } = localEventBus;
    public IDistributedEventBus DistributedEventBus { get; set; } = distributedEventBus;
    protected IObjectMapper EntityToEtoMapper { get; } = entityToEtoMapper;
    protected DistributedEntityEventOptions DistributedEntityEventOptions { get; } = distributedEntityEventOptions.Value;

    public virtual void AddEntityCreatedEvent(object entity)
    {
        if (!ShouldPublishEventForEntity(entity)) return;

        TriggerEventWithEntity(
            LocalEventBus,
            typeof(EntityCreatedEventData<>),
            entity,
            entity
        );

        if (DistributedEntityEventOptions.EtoMappings.TryGetValue(entity.GetType(), out var type))
        {
            var eto = EntityToEtoMapper.Map(entity, entity.GetType(), type);
            TriggerEventWithEntity(
                DistributedEventBus,
                typeof(EntityCreatedEto<>),
                eto,
                entity
            );
        }
    }

    private bool ShouldPublishEventForEntity(object entity)
    {
        return DistributedEntityEventOptions
            .AutoEventSelectors
            .Contains(entity.GetType());
    }

    public virtual void AddEntityUpdatedEvent(object entity)
    {
        if (!ShouldPublishEventForEntity(entity)) return;

        TriggerEventWithEntity(
            LocalEventBus,
            typeof(EntityUpdatedEventData<>),
            entity,
            entity
        );
        if (DistributedEntityEventOptions.EtoMappings.TryGetValue(entity.GetType(), out var type))
        {
            var eto = EntityToEtoMapper.Map(entity, entity.GetType(), type);
            TriggerEventWithEntity(
                DistributedEventBus,
                typeof(EntityUpdatedEto<>),
                eto,
                entity
            );
        }
    }

    public virtual void AddEntityDeletedEvent(object entity)
    {
        if (!ShouldPublishEventForEntity(entity)) return;

        TriggerEventWithEntity(
            LocalEventBus,
            typeof(EntityDeletedEventData<>),
            entity,
            entity
        );
        if (DistributedEntityEventOptions.EtoMappings.TryGetValue(entity.GetType(), out var type))
        {
            var eto = EntityToEtoMapper.Map(entity, entity.GetType(), type);
            TriggerEventWithEntity(
                DistributedEventBus,
                typeof(EntityDeletedEto<>),
                eto,
                entity
            );
        }
    }

    public async Task FlushEventBuffer()
    {
        var buffer = bufferStore.GetBuffer();
        if (buffer != null)
        {
            await buffer.Flush(LocalEventBus, DistributedEventBus);
        }
    }


    #region Store



    protected virtual void TriggerEventWithEntity(
        IEventBus eventPublisher,
        Type genericEventType,
        object entityOrEto,
        object originalEntity)
    {
        var entityType = entityOrEto.GetType();
        var eventType = genericEventType.MakeGenericType(entityType);
        var eventData = Activator.CreateInstance(eventType, entityOrEto)!;


        var eventRecord = new TransactionEventRecord(eventType, eventData, originalEntity);

        var buffer = bufferStore.GetOrNewBuffer();
        buffer.Records.Add(eventRecord);
        if (eventPublisher == DistributedEventBus)
        {
            AddOrReplaceEvent(buffer.DistributedEvents, buffer.DistributedEventsHash, eventRecord);
        }
        else
        {
            AddOrReplaceEvent(buffer.LocalEvents, buffer.LocalEventsHash, eventRecord);
        }
    }
    public virtual void AddOrReplaceEvent(List<TransactionEventRecord> events, HashSet<int> eventHashSet, TransactionEventRecord eventRecord)
    {
        var hash = eventRecord.GetEventHashCode();
        if (hash == null)
        {
            events.Add(eventRecord);
            return;
        }
        if (eventHashSet.Add(hash.Value))
        {
            events.Add(eventRecord);
        }
        else
        {
            //若产生Hash碰撞
            var foundIndex = events.FindIndex(p => IsSameEntityEventRecord(p, eventRecord));
            if (foundIndex < 0)
            {
                events.Add(eventRecord);
            }
        }
    }
    public bool IsSameEntityEventRecord(TransactionEventRecord record1, TransactionEventRecord record2)
    {
        if (record1.EventType != record2.EventType)
        {
            return false;
        }

        if (record1.OriginEntity is not IMoEntity record1OriginalEntity || record2.OriginEntity is not IMoEntity record2OriginalEntity)
        {
            return false;
        }

        return EntityHelper.EntityEquals(record1OriginalEntity, record2OriginalEntity);
    }
    #endregion


}
