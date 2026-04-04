using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Extensions;

namespace Monica.Repository.UnitOfWork.Models;

public class TransactionEventRecord(
    Type eventType,
    object eventData,
    object originEntity)
{
    public object EventData { get; } = eventData;

    public Type EventType { get; } = eventType;

    public object OriginEntity { get; set; } = originEntity;

    public int? GetEventHashCode()
    {
        if (OriginEntity is not IEntity entity || EntityHelper.HasDefaultKeys(entity))
            return null;
        var keys = entity.GetKeys();
        var length = keys.Length;
        var entityName = EventType.Name;
        var hashCode = HashCode.Combine(length, entityName);
        return entity.GetKeys().Aggregate(hashCode, HashCode.Combine);
    }
}

