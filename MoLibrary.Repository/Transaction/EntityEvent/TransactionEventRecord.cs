using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Repository.Extensions;

namespace MoLibrary.Repository.Transaction.EntityEvent;

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
        if (OriginEntity is not IMoEntity entity || EntityHelper.HasDefaultKeys(entity))
            return null;
        var keys = entity.GetKeys();
        var length = keys.Length;
        var entityName = EventType.Name;
        var hashCode = HashCode.Combine(length, entityName);
        return entity.GetKeys().Aggregate(hashCode, HashCode.Combine);
    }
}

