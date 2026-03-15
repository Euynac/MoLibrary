using System.Reflection;
using System.Xml.XPath;

namespace Monica.Core.Features.MoXmlDocumentation;

/// <summary>
/// Provides access to XML documentation generated for assemblies.
/// </summary>
public interface IXmlDocumentationService
{
    /// <summary>
    /// Gets XML documentation for a method.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns>The method documentation, or <see langword="null" /> when none is available.</returns>
    XmlMethodDocumentation? GetMethodDocumentation(MethodInfo method);

    /// <summary>
    /// Gets the summary documentation for a type.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>The type summary, or <see langword="null" /> when none is available.</returns>
    string? GetTypeDocumentation(Type type);

    /// <summary>
    /// Clears the cached XML documents.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets information about the currently cached XML documents.
    /// </summary>
    /// <returns>The cached XML document information.</returns>
    IReadOnlyList<XmlDocumentCacheInfo> GetCachedDocuments();
}

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

/// <summary>
/// Metadata about a cached XML document.
/// </summary>
public class XmlDocumentCacheInfo
{
    /// <summary>
    /// Assembly name.
    /// </summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// XML file path.
    /// </summary>
    public string XmlFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Cached XPath document.
    /// </summary>
    public XPathDocument? Document { get; set; }

    /// <summary>
    /// Time when the document was cached.
    /// </summary>
    public DateTime CachedAt { get; set; }
}
