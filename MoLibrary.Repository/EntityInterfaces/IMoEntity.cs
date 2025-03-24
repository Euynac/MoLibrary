namespace MoLibrary.Repository.EntityInterfaces;

/// <summary>
/// Defines an entity. It's primary key may not be "Id" or it may have a composite primary key.
/// Use <see cref="IMoEntity{TKey}"/> where possible for better integration to repositories and other structures in the framework.
/// </summary>
public interface IMoEntity
{
    /// <summary>
    /// Returns an array of ordered keys for this entity.
    /// </summary>
    /// <returns></returns>
    object?[] GetKeys();
    /// <summary>
    /// 自动设置Id；若有子表需要设置，则重写该方法
    /// </summary>
    /// <param name="notSetWhenNotDefault">当已有值时不设置</param>
    public void AutoSetNewId(bool notSetWhenNotDefault = false);
}

/// <summary>
/// Defines an entity with a single primary key with "Id" property.
/// </summary>
/// <typeparam name="TKey">Type of the primary key of the entity</typeparam>
public interface IMoEntity<TKey> : IMoEntity
{
    /// <summary>
    /// Unique identifier for this entity.
    /// </summary>
    TKey Id { get; }
    /// <summary>
    /// 设置实体Id
    /// </summary>
    /// <param name="key"></param>
    public void SetNewId(TKey key);
}


