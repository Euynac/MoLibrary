using System.Text.Json.Serialization;
using Monica.Tool.Extensions;

namespace Monica.Repository.Entity.Abstractions;

[Serializable]
public abstract class Entity : IEntity
{
    public override string ToString()
    {
        return $"[ENTITY: {GetType().Name}] Keys = {GetKeys().JoinAsString(", ")}";
    }
    public abstract object?[] GetKeys();

    public virtual void AutoSetNewId(bool notSetWhenNotDefault = false)
    {
        throw new NotImplementedException("请自己实现新ID设置方法");
    }

}

[Serializable]
public abstract class Entity<TKey> : Entity, IEntity<TKey>
{
    /// <summary>
    /// Id of the entity.
    /// </summary>
    [JsonInclude]
   // [Key]
    public TKey Id { get; set; } = default!;

    public override string ToString()
    {
        return $"[ENTITY: {GetType().Name}] Id = {Id}";
    }

    public override object?[] GetKeys()
    {
        return [Id];
    }
}
