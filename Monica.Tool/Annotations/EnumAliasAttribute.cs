namespace Monica.Tool.Annotations;

/// <summary>
/// Declares alternative text values that can map to an enum member.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class EnumAliasAttribute(params string[] names) : Attribute
{
    /// <summary>
    /// Gets the accepted aliases for the annotated enum member.
    /// </summary>
    public string[] Names { get; } = names;
}
