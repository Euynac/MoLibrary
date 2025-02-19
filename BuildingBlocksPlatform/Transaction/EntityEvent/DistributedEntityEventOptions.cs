namespace BuildingBlocksPlatform.Transaction.EntityEvent;

public class DistributedEntityEventOptions
{
    public HashSet<Type> AutoEventSelectors { get; } = [];

    public Dictionary<Type, Type> EtoMappings { get;  } = [];

    /// <summary>
    /// 添加本地事件映射
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public void AddLocalEntityEvent<TEntity>() where TEntity : class 
    {
        AutoEventSelectors.Add(typeof(TEntity));
    }
    /// <summary>
    /// 添加分布式事件映射（同时也会添加本地事件）
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TEntityEto"></typeparam>
    public void AddDistributedEntityEvent<TEntity, TEntityEto>() where TEntity : class where TEntityEto : class
    {
        EtoMappings.Add(typeof(TEntity), typeof(TEntityEto));
        AutoEventSelectors.Add(typeof(TEntity));
    }
}
