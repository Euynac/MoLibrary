using Monica.Modules;

namespace Monica.AutoModel.Annotations;
[AttributeUsage(AttributeTargets.Property)]
public class AutoFieldAttribute : Attribute
{
    /// <summary>
    /// Additional activation names for the field. The reflected name is included by default.
    /// </summary>
    public List<string>? ActivateNames { get; set; }

    /// <summary>
    /// Display name of the field.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Whether to use the field title as an activation name. If <c>null</c>, the parent setting is used.
    /// </summary>
    [Obsolete("Not implemented yet.")]
    public bool? TitleAsActivateName { get; set; }

    /// <summary>
    /// Whether to ignore this field during full-field fuzzy searches.
    /// </summary>
    public bool IgnoreFuzzColumn { get; set; }

    /// <summary>
    /// Whether this field is required in filter expressions.
    /// </summary>
    [Obsolete("Not implemented yet.")]
    public bool IsRequired { get; set; }

    /// <summary>
    /// Ignores this field and skips AutoModel field generation.
    /// </summary>
    public bool Ignore { get; set; }

    /// <summary>
    /// Whether to ignore the prefix for this field. If <c>null</c>, the parent setting is used.
    /// </summary>
    public bool? EnableIgnorePrefix { get; set; }
}
