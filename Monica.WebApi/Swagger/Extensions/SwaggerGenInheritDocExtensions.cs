using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Monica.WebApi.Swagger.Extensions;

/// <summary>
/// Extension methods for loading XML comments into Swagger while resolving supported <c>inheritdoc</c> patterns.
/// </summary>
public static class SwaggerGenInheritDocExtensions
{
    /// <summary>
    /// Includes XML comments from multiple files and replaces <c>inheritdoc cref="..."</c> references
    /// when the referenced documentation is available in the same XML document set.
    /// </summary>
    /// <param name="options">The Swagger generator options.</param>
    /// <param name="xmlFilePaths">XML documentation files to load.</param>
    /// <param name="includeControllerXmlComments">Whether controller comments should be included.</param>
    /// <param name="logger">Optional logger used when XML files cannot be loaded.</param>
    public static void IncludeXmlCommentsWithInheritDoc(
        this SwaggerGenOptions options,
        IEnumerable<string> xmlFilePaths,
        bool includeControllerXmlComments = false,
        ILogger? logger = null)
    {
        var xmlDocuments = LoadXmlDocuments(xmlFilePaths, logger);
        if (xmlDocuments.Count == 0)
        {
            return;
        }

        var memberDocumentation = BuildMemberDocumentationMap(xmlDocuments);
        foreach (var xmlDocument in xmlDocuments)
        {
            ResolveInheritDocElements(xmlDocument, memberDocumentation);
            options.IncludeXmlComments(() => new XPathDocument(xmlDocument.CreateReader()), includeControllerXmlComments);
        }
    }

    private static List<XDocument> LoadXmlDocuments(IEnumerable<string> xmlFilePaths, ILogger? logger)
    {
        var xmlDocuments = new List<XDocument>();
        foreach (var xmlFilePath in xmlFilePaths)
        {
            if (!File.Exists(xmlFilePath))
            {
                continue;
            }

            try
            {
                xmlDocuments.Add(XDocument.Load(xmlFilePath));
            }
            catch (Exception exception)
            {
                logger?.LogWarning(exception, "Failed to load XML documentation file: {FilePath}", xmlFilePath);
            }
        }

        return xmlDocuments;
    }

    private static Dictionary<string, XElement> BuildMemberDocumentationMap(IEnumerable<XDocument> xmlDocuments)
    {
        var memberDocumentation = new Dictionary<string, XElement>();
        foreach (var xmlDocument in xmlDocuments)
        {
            var members = xmlDocument.XPathSelectElements("/doc/members/member[@name and not(.//inheritdoc)]");
            foreach (var member in members)
            {
                var memberName = member.Attribute("name")?.Value;
                if (memberName is null || memberDocumentation.ContainsKey(memberName))
                {
                    continue;
                }

                memberDocumentation.Add(memberName, member);
            }
        }

        return memberDocumentation;
    }

    private static void ResolveInheritDocElements(
        XDocument xmlDocument,
        IReadOnlyDictionary<string, XElement> memberDocumentation)
    {
        var membersWithInheritDoc = xmlDocument.XPathSelectElements("/doc/members/member[.//inheritdoc[@cref]]");
        foreach (var member in membersWithInheritDoc)
        {
            var inheritDocElements = member
                .Descendants("inheritdoc")
                .Where(element => element.Attribute("cref") is not null)
                .ToList();

            foreach (var inheritDocElement in inheritDocElements)
            {
                var cref = inheritDocElement.Attribute("cref")?.Value;
                if (cref is null || !memberDocumentation.TryGetValue(cref, out var referencedMember))
                {
                    continue;
                }

                var parentElement = inheritDocElement.Parent;
                if (parentElement is null)
                {
                    continue;
                }

                var referencedElement = referencedMember.Element(parentElement.Name);
                if (referencedElement is null)
                {
                    inheritDocElement.Remove();
                    continue;
                }

                parentElement.ReplaceNodes(referencedElement.Nodes());
            }
        }
    }
}
