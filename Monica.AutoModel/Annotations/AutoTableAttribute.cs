using Monica.Modules;

namespace Monica.AutoModel.Annotations;
[AttributeUsage(AttributeTargets.Class)]
public class AutoTableAttribute : Attribute
{
    /// <summary>
    /// Controls whether the table uses active mode.
    /// When <c>false</c>, passive mode is used by default.
    /// When <c>null</c>, the module-level setting is used.
    /// </summary>
    public bool? ActiveMode { get; set; }
    /// <summary>
    /// Table name.
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Whether to ignore prefixes. If <c>null</c>, the parent setting is used.
    /// </summary>
    public bool? EnableIgnorePrefix { get; set; }
}
