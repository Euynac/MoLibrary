namespace Monica.Framework.AlterChain.Attributes;

/// <summary>
/// Marking entity classes requires generating the corresponding AlterItemData class and Apply method
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateAlterItemDataAttribute : Attribute
{
    /// <summary>
    /// The namespace of the generated AlterItemData class. If not specified, the namespace of the entity class is used.
    /// </summary>
    public string? Namespace { get; set; }
    
    /// <summary>
    /// Generated AlterItemData class name, if not specified {EntityName}AlterItemData is used
    /// </summary>
    public string? ClassName { get; set; }
    
    /// <summary>
    /// Whether to include debugging information comments
    /// </summary>
    public bool IncludeDebugInfo { get; set; } = false;
}