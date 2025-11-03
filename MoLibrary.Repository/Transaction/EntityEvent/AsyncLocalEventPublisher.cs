using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.MoMapper;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Repository.Extensions;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Repository.Transaction.EntityEvent;


public class AsyncEventBuffer
{
    public List<TransactionEventRecord> Records { get; set; } = [];
    public List<TransactionEventRecord> DistributedEvents { get; } = [];
    public List<TransactionEventRecord> LocalEvents { get; } = [];

    public HashSet<int> DistributedEventsHash { get; } = [];
    public HashSet<int> LocalEventsHash { get; } = [];

    public async Task Flush(IMoLocalEventBus eventBus, IMoDistributedEventBus distributedEventBus)
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
/// <summary>
/// Used to trigger entity change events.
/// </summary>
public class AsyncLocalEventPublisher(
    IMoMapper entityToEtoMapper,
    IOptions<DistributedEntityEventOptions> distributedEntityEventOptions,
    IMoLocalEventBus localEventBus,
    IMoDistributedEventBus distributedEventBus,
    ILogger<AsyncLocalEventPublisher> logger, IAsyncLocalEventStore bufferStore) : IAsyncLocalEventPublisher
{
    /// <summary>
    /// Gets or sets the local event bus
    /// </summary>
    public IMoLocalEventBus LocalEventBus { get; set; } = localEventBus;
    
    /// <summary>
    /// Gets or sets the distributed event bus
    /// </summary>
    public IMoDistributedEventBus DistributedEventBus { get; set; } = distributedEventBus;
    
    /// <summary>
    /// Gets the entity to ETO mapper
    /// </summary>
    protected IMoMapper EntityToEtoMapper { get; } = entityToEtoMapper;
    
    /// <summary>
    /// Gets the distributed entity event options
    /// </summary>
    protected DistributedEntityEventOptions DistributedEntityEventOptions { get; } = distributedEntityEventOptions.Value;

    /// <summary>
    /// Adds an entity created event to the event buffer
    /// </summary>
    /// <param name="entity">The entity that was created</param>
    public virtual void AddEntityCreatedEvent(object entity)
    {
        if (!ShouldPublishEventForEntity(entity, out var eventOption)) return;
        
        // Check if create events are disabled for this entity
        if (eventOption.DisabledAutoEntityEventType.HasFlag(EDisabledAutoEntityEventType.Create))
            return;

        if (eventOption.EnableLocalEvent)
        {
            TriggerEventWithEntity(
                LocalEventBus,
                typeof(EntityCreatedEventData<>),
                entity,
                entity
            );
        }

        if (eventOption.EnableDistributedEvent)
        {
            var eto = eventOption.EtoMappingType != null ? EntityToEtoMapper.Map(entity, entity.GetType(), eventOption.EtoMappingType) : entity;

            TriggerEventWithEntity(
                DistributedEventBus,
                typeof(EntityCreatedEto<>),
                eto,
                entity
            );
        }
    }

    /// <summary>
    /// Determines if events should be published for the given entity
    /// </summary>
    /// <param name="entity">The entity to check</param>
    /// <param name="eventOption">The event options for the entity if found</param>
    /// <returns>True if events should be published, false otherwise</returns>
    private bool ShouldPublishEventForEntity(object entity,[NotNullWhen(true)] out EntityEventOption? eventOption)
    {
        return DistributedEntityEventOptions
            .AutoEventOptionDict
            .TryGetValue(entity.GetType(), out eventOption);
    }

    /// <summary>
    /// Adds an entity updated event to the event buffer
    /// </summary>
    /// <param name="entity">The entity that was updated</param>
    public virtual void AddEntityUpdatedEvent(object entity)
    {
        if (!ShouldPublishEventForEntity(entity, out var eventOption)) return;
        
        // Check if update events are disabled for this entity
        if (eventOption.DisabledAutoEntityEventType.HasFlag(EDisabledAutoEntityEventType.Update))
            return;

        if (eventOption.EnableLocalEvent)
        {
            TriggerEventWithEntity(
                LocalEventBus,
                typeof(EntityUpdatedEventData<>),
                entity,
                entity
            );
        }

        if (eventOption.EnableDistributedEvent)
        {
            var eto = eventOption.EtoMappingType != null ? EntityToEtoMapper.Map(entity, entity.GetType(), eventOption.EtoMappingType) : entity;

            TriggerEventWithEntity(
                DistributedEventBus,
                typeof(EntityUpdatedEto<>),
                eto,
                entity
            );
        }
    }

    /// <summary>
    /// Adds an entity deleted event to the event buffer
    /// </summary>
    /// <param name="entity">The entity that was deleted</param>
    public virtual void AddEntityDeletedEvent(object entity)
    {
        if (!ShouldPublishEventForEntity(entity, out var eventOption)) return;
        
        // Check if delete events are disabled for this entity
        if (eventOption.DisabledAutoEntityEventType.HasFlag(EDisabledAutoEntityEventType.Delete))
            return;

        if (eventOption.EnableLocalEvent)
        {
            TriggerEventWithEntity(
                LocalEventBus,
                typeof(EntityDeletedEventData<>),
                entity,
                entity
            );
        }

        if (eventOption.EnableDistributedEvent)
        {
            var eto = eventOption.EtoMappingType != null ? EntityToEtoMapper.Map(entity, entity.GetType(), eventOption.EtoMappingType) : entity;

            TriggerEventWithEntity(
                DistributedEventBus,
                typeof(EntityDeletedEto<>),
                eto,
                entity
            );
        }
    }

    /// <summary>
    /// Flushes the event buffer, publishing all pending events
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task FlushEventBuffer()
    {
        var buffer = bufferStore.GetBuffer();
        if (buffer != null)
        {
            await buffer.Flush(LocalEventBus, DistributedEventBus);
        }
    }


    #region Store

    /// <summary>
    /// Triggers an event with the specified entity
    /// </summary>
    /// <param name="eventPublisher">The event publisher to use</param>
    /// <param name="genericEventType">The generic event type</param>
    /// <param name="entityOrEto">The entity or ETO object</param>
    /// <param name="originalEntity">The original entity</param>
    protected virtual void TriggerEventWithEntity(
        IMoEventBus eventPublisher,
        Type genericEventType,
        object entityOrEto,
        object originalEntity)
    {
        var entityType = entityOrEto.GetType();
        var eventType = genericEventType.MakeGenericType(entityType);
        var eventData = Activator.CreateInstance(eventType, entityOrEto)!;


        var eventRecord = new TransactionEventRecord(eventType, eventData, originalEntity);

        var buffer = bufferStore.GetOrNewBuffer();
        //buffer.Records.Add(eventRecord); //暂时用于测试
        if (eventPublisher == DistributedEventBus)
        {
            AddOrReplaceEvent(buffer.DistributedEvents, buffer.DistributedEventsHash, eventRecord);
        }
        else
        {
            AddOrReplaceEvent(buffer.LocalEvents, buffer.LocalEventsHash, eventRecord);
        }
    }
    
    /// <summary>
    /// Adds or replaces an event in the event list
    /// </summary>
    /// <param name="events">The list of events</param>
    /// <param name="eventHashSet">The hash set of event hashes</param>
    /// <param name="eventRecord">The event record to add or replace</param>
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
    
    /// <summary>
    /// Determines if two event records refer to the same entity event
    /// </summary>
    /// <param name="record1">The first event record</param>
    /// <param name="record2">The second event record</param>
    /// <returns>True if the records refer to the same entity event, false otherwise</returns>
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
