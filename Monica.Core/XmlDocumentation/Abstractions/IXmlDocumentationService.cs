using System.Reflection;
using Monica.Core.XmlDocumentation.Models;

namespace Monica.Core.XmlDocumentation.Abstractions;

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
