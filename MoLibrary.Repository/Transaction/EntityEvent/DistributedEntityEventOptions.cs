using MoLibrary.Repository.EntityInterfaces;

namespace MoLibrary.Repository.Transaction.EntityEvent;

public class DistributedEntityEventOptions
{
    /// <summary>
    /// Dictionary that stores entity types and their event configuration options
    /// </summary>
    public Dictionary<Type, EntityEventOption> AutoEventOptionDict { get; } = [];

    /// <summary>
    /// Adds local event mapping for an entity type
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="disabledAutoEntityEventType">Optional parameter to specify which auto entity event types should be disabled</param>
    public void AddLocalEntityEvent<TEntity>(EDisabledAutoEntityEventType? disabledAutoEntityEventType = null) where TEntity : class
    {
        var entityType = typeof(TEntity);
        if (AutoEventOptionDict.TryGetValue(entityType, out var option))
        {
            option.EnableLocalEvent = true;
            if (disabledAutoEntityEventType.HasValue)
            {
                option.DisabledAutoEntityEventType = disabledAutoEntityEventType.Value;
            }
        }
        else
        {
            AutoEventOptionDict[entityType] = new EntityEventOption
            {
                EnableLocalEvent = true,
                EnableDistributedEvent = false,
                DisabledAutoEntityEventType = disabledAutoEntityEventType ?? EDisabledAutoEntityEventType.None
            };
        }
    }

    /// <summary>
    /// Adds distributed event mapping with an explicit ETO type. It will replace the existing mapping if it exists.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TEntityEto">The Event Transfer Object type</typeparam>
    /// <param name="enableLocalEvent">Indicates whether to enable local events as well</param>
    /// <param name="disabledAutoEntityEventType">Optional parameter to specify which auto entity event types should be disabled</param>
    public void AddDistributedEntityEvent<TEntity, TEntityEto>(bool enableLocalEvent = true, EDisabledAutoEntityEventType? disabledAutoEntityEventType = null) 
        where TEntity : class, IMoEntity 
        where TEntityEto : class
    {
        var entityType = typeof(TEntity);
        var etoType = typeof(TEntityEto);
        if (AutoEventOptionDict.TryGetValue(entityType, out var option))
        {
            option.EnableDistributedEvent = true;
            option.EtoMappingType = entityType == etoType ? null : etoType;
            if (enableLocalEvent)
            {
                option.EnableLocalEvent = true;
            }
            if (disabledAutoEntityEventType.HasValue)
            {
                option.DisabledAutoEntityEventType = disabledAutoEntityEventType.Value;
            }
        }
        else
        {
            AutoEventOptionDict[entityType] = new EntityEventOption
            {
                EnableLocalEvent = enableLocalEvent,
                EnableDistributedEvent = true,
                EtoMappingType = entityType == etoType ? null : etoType,
                DisabledAutoEntityEventType = disabledAutoEntityEventType ?? EDisabledAutoEntityEventType.None
            };
        }
    }

    /// <summary>
    /// Adds distributed event mapping using the entity type itself as the ETO type
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="enableLocalEvent">Indicates whether to enable local events as well</param>
    /// <param name="disabledAutoEntityEventType">Optional parameter to specify which auto entity event types should be disabled</param>
    public void AddDistributedEntityEvent<TEntity>(bool enableLocalEvent = true, EDisabledAutoEntityEventType? disabledAutoEntityEventType = null) 
        where TEntity : class, IMoEntity
    {
        AddDistributedEntityEvent<TEntity, TEntity>(enableLocalEvent, disabledAutoEntityEventType);
    }
}

/// <summary>
/// Configuration options for entity events
/// </summary>
public class EntityEventOption
{
    /// <summary>
    /// Gets or sets whether local events are enabled for the entity
    /// </summary>
    public bool EnableLocalEvent { get; set; }

    /// <summary>
    /// Gets or sets whether distributed events are enabled for the entity
    /// </summary>
    public bool EnableDistributedEvent { get; set; }

    /// <summary>
    /// Gets or sets the ETO (Event Transfer Object) mapping type for distributed events
    /// </summary>
    public Type? EtoMappingType { get; set; }

    /// <summary>
    /// Gets or sets the disabled auto entity event types
    /// </summary>
    public EDisabledAutoEntityEventType DisabledAutoEntityEventType { get; set; }
}

/// <summary>
/// Disabled auto entity event types
/// </summary>
[Flags]
public enum EDisabledAutoEntityEventType
{
    /// <summary>
    /// No events are disabled
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Create events are disabled
    /// </summary>
    Create = 1 << 0,
    
    /// <summary>
    /// Update events are disabled
    /// </summary>
    Update = 1 << 1,
    
    /// <summary>
    /// Delete events are disabled
    /// </summary>
    Delete = 1 << 2
}