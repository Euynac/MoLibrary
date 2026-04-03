namespace Monica.Framework.XmlDocumentation.Models;

/// <summary>
/// XML documentation extracted for a method.
/// </summary>
public class XmlMethodDocumentation
{
    /// <summary>
    /// Method summary text.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Return value description.
    /// </summary>
    public string? Returns { get; set; }

    /// <summary>
    /// Remarks text.
    /// </summary>
    public string? Remarks { get; set; }

    /// <summary>
    /// Parameter descriptions keyed by parameter name.
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>
    /// Exception descriptions keyed by exception type name.
    /// </summary>
    public Dictionary<string, string> Exceptions { get; set; } = new();
}
