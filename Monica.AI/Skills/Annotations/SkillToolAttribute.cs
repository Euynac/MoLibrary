namespace Monica.AI.Skills.Annotations;

/// <summary>
/// Marks a Monica skill method as an AI-callable script and supplies optional tool metadata.
/// </summary>
[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Parameter,
    AllowMultiple = false,
    Inherited = false)]
public sealed class SkillToolAttribute : Attribute
{
    /// <summary>
    /// Optional script name override for methods. Parameters ignore this value.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Optional method or parameter description override used in generated AI tool metadata.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Excludes the annotated method from AI tool registration. Parameters ignore this value.
    /// </summary>
    public bool Disabled { get; set; }
}
