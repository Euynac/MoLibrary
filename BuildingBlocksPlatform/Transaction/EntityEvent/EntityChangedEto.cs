using BuildingBlocksPlatform.EventBus.Attributes;

namespace BuildingBlocksPlatform.Transaction.EntityEvent;

[Serializable]
[GenericEventName(Postfix = ".Created")]
public class EntityCreatedEto<TEntityEto>(TEntityEto entity)
{
    public TEntityEto Entity { get; set; } = entity;
}
[Serializable]
[GenericEventName(Postfix = ".Deleted")]
public class EntityDeletedEto<TEntityEto>(TEntityEto entity)
{
    public TEntityEto Entity { get; set; } = entity;
}
[Serializable]
[GenericEventName(Postfix = ".Updated")]
public class EntityUpdatedEto<TEntityEto>(TEntityEto entity)
{
    public TEntityEto Entity { get; set; } = entity;
}
