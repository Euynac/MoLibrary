using System.Xml.XPath;

namespace Monica.Core.Features.MoXmlDocumentation;

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
