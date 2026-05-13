using Monica.Configuration.Models;

namespace Monica.Configuration.Annotations;

/// <summary>
/// Marks a CLR options type as a Monica-managed configuration definition.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ConfigurationAttribute : Attribute
{
    private string? _sectionPath;

    /// <summary>
    /// Initializes a new configuration attribute without an explicit section path.
    /// The scanner will derive the binding section from the type identity.
    /// </summary>
    public ConfigurationAttribute()
    {
    }

    /// <summary>
    /// Initializes a new configuration attribute with an explicit binding section path.
    /// </summary>
    /// <param name="sectionPath">The root Microsoft configuration section path.</param>
    public ConfigurationAttribute(string sectionPath)
    {
        _sectionPath = sectionPath;
    }

    /// <summary>
    /// Gets the root Microsoft configuration section path.
    /// </summary>
    public string? SectionPath
    {
        get => _sectionPath;
        set => _sectionPath = value;
    }

    /// <summary>
    /// Gets or sets the stable definition key. When omitted, the scanner derives one from the CLR type.
    /// </summary>
    public string? DefinitionKey { get; set; }

    /// <summary>
    /// Gets or sets the display name shown in management tools.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the developer-facing description shown in generated documentation.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the owning Monica module or application component.
    /// </summary>
    public string? OwnerModule { get; set; }

    /// <summary>
    /// Gets or sets a developer-defined category used for grouping configuration definitions.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the default reload behavior for nodes under this definition.
    /// </summary>
    public ConfigurationReloadBehavior ReloadBehavior { get; set; } = ConfigurationReloadBehavior.OnlineReloadable;

}
