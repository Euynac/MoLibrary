using Monica.Framework.Core.Interfaces;

namespace Monica.Framework.Core.Attributes;

/// <summary>
/// Project unit information
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class UnitInfoAttribute(string name) : Attribute, IUnitCachedAttribute
{
    /// <summary>
    /// The project unit name will be displayed on the relevant UI interface.
    /// </summary>
    public string Name { get; set; } = name;

    /// <summary>
    /// Project unit author, usually used to identify the creator or maintainer or person responsible for the unit.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Related business groups, or requirement IDs, module IDs, etc., are used for UI interface related project units.
    /// </summary>
    public string[]? Group { get; set; }

    /// <summary>
    /// Project unit description, usually used to briefly describe the function or purpose of the unit. If this attribute is empty, the summary content in the XML comment is read.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Markdown document description, link or path, usually used to provide more detailed documentation.
    /// </summary>
    /// <remarks>Custom syntax: @User Management.md#Permission Control can generate the hyperlink address of the corresponding configured document service (not implemented yet)</remarks>
    public string? MarkdownDocs { get; set; }
}