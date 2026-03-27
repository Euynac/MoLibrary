namespace Monica.Framework.Features.AlterChain;

public interface IMoTracingDataAlterItemData<in TEntity> where TEntity : class, IMoTracingDataEntity
{
    /// <summary>
    /// Apply current changes
    /// </summary>
    /// <param name="entity"></param>
    void Apply(TEntity entity);
    
    /// <summary>
    /// Get current change information
    /// </summary>
    /// <param name="entity">If this value is passed in, the entity value related to the change will be returned.</param>
    /// <returns></returns>
    IEnumerable<PropertyAlterData> GetChanges(TEntity? entity = null);
}

/// <summary>
/// Property change information
/// </summary>
public class PropertyAlterData
{
    /// <summary>
    /// Change the display name (the Title of AlterItemPropertyAttribute is used first, otherwise it is PropertyName)
    /// </summary>
    public required string DisplayName { get; set; }
    /// <summary>
    /// Change attribute name
    /// </summary>
    public required string PropertyName { get; set; }
    /// <summary>
    /// The associated value from the Entity
    /// </summary>
    public object? OldValue { get; set; }
    /// <summary>
    /// The associated value from PropertyAlterData
    /// </summary>
    public object? NewValue { get; set; }
}