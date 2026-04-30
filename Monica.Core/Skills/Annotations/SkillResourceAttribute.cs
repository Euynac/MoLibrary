namespace Monica.Core.Skills.Annotations;

/// <summary>
/// Marks a Monica skill property or method as a readable AI skill resource.
/// </summary>
/// <remarks>
/// Resources provide supplementary content for a skill, such as reference text, templates, examples, or
/// computed context. Resource methods may accept <see cref="IServiceProvider" /> and
/// <see cref="CancellationToken" /> parameters; other parameters cannot be supplied by the skill runtime.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SkillResourceAttribute : Attribute
{
    /// <summary>
    /// Optional resource name override. When omitted, Monica derives a kebab-case name from the member name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Optional resource description override used in generated AI skill metadata.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Excludes the annotated member from AI resource registration.
    /// </summary>
    public bool Disabled { get; set; }
}
