using System.Text.Json.Serialization;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Repository.EntityInterfaces;

[Serializable]
public abstract class MoEntity : IMoEntity
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
public abstract class MoEntity<TKey> : MoEntity, IMoEntity<TKey>
{
    /// <summary>
    /// Id of the entity.
    /// </summary>
    [JsonInclude]
    public TKey Id { get; protected set; } = default!;

    public override string ToString()
    {
        return $"[ENTITY: {GetType().Name}] Id = {Id}";
    }
    public virtual void SetNewId(TKey key)
    {
        Id = key;
    }
    public override object?[] GetKeys()
    {
        return [Id];
    }
}
