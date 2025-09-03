using System.Reflection;

namespace MoLibrary.Framework.Features.AlterChain;

public interface IMoTracingDataAlterItemData<in TEntity> where TEntity : class, IMoTracingDataEntity
{
    /// <summary>
    /// 应用当前变更
    /// </summary>
    /// <param name="entity"></param>
    void Apply(TEntity entity);
    
    /// <summary>
    /// 获取当前变更信息
    /// </summary>
    /// <param name="entity">如果传入此值将会返回变更相关的实体值</param>
    /// <returns></returns>
    IEnumerable<PropertyAlterData> GetChanges(TEntity? entity = null);
}

/// <summary>
/// 属性变更信息
/// </summary>
public class PropertyAlterData
{
    /// <summary>
    /// 变更显示名（优先使用 AlterItemPropertyAttribute 的Title，否则就是PropertyName）
    /// </summary>
    public required string DisplayName { get; set; }
    /// <summary>
    /// 变更属性名
    /// </summary>
    public required string PropertyName { get; set; }
    /// <summary>
    /// 来自于 Entity 的相关的值
    /// </summary>
    public object? OldValue { get; set; }
    /// <summary>
    /// 来自于 PropertyAlterData 的相关的值
    /// </summary>
    public object? NewValue { get; set; }
}