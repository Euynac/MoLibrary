namespace MoLibrary.Framework.Features.AlterChain.Attributes;

/// <summary>
/// 标记实体类需要生成对应的 AlterItemData 类和 Apply 方法
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateAlterItemDataAttribute : Attribute
{
    /// <summary>
    /// 生成的 AlterItemData 类的命名空间，如果不指定则使用实体类的命名空间
    /// </summary>
    public string? Namespace { get; set; }
    
    /// <summary>
    /// 生成的 AlterItemData 类名，如果不指定则使用 {EntityName}AlterItemData
    /// </summary>
    public string? ClassName { get; set; }
    
    /// <summary>
    /// 是否包含调试信息注释
    /// </summary>
    public bool IncludeDebugInfo { get; set; } = false;
}