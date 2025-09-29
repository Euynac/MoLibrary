using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MoLibrary.DomainDrivenDesign.Swagger;

public static class InheritDocXmlCommentsFilter
{
    public static void IncludeXmlCommentsWithInheritDoc(this SwaggerGenOptions options,
        IEnumerable<string> xmlFilePaths,
        bool includeControllerXmlComments = false,
        ILogger? logger = null)
    {
        var xmlDocList = new List<XDocument>();

        // Load XML documents
        foreach (var xmlFilePath in xmlFilePaths)
        {
            if (File.Exists(xmlFilePath))
            {
                try
                {
                    xmlDocList.Add(XDocument.Load(xmlFilePath));
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to load XML documentation file: {FilePath}", xmlFilePath);
                }
            }
        }

        if (!xmlDocList.Any()) return;

        var members = new Dictionary<string, XElement>();

        // Map identifier to element
        foreach (var xmlDoc in xmlDocList)
        {
            var memberElementList = xmlDoc.XPathSelectElements("/doc/members/member[@name and not(.//inheritdoc)]");
            foreach (var memberElement in memberElementList)
            {
                var nameAttr = memberElement.Attribute("name");
                if (nameAttr?.Value != null && !members.ContainsKey(nameAttr.Value))
                {
                    members.Add(nameAttr.Value, memberElement);
                }
            }
        }

        // Replace inheritdoc
        foreach (var xmlDoc in xmlDocList)
        {
            var memberElementList = xmlDoc.XPathSelectElements("/doc/members/member[.//inheritdoc[@cref]]");
            foreach (var memberElement in memberElementList)
            {
                var inheritdocElements = memberElement.Descendants("inheritdoc").Where(e => e.Attribute("cref") != null).ToList();

                foreach (var inheritdocElement in inheritdocElements)
                {
                    var crefAttr = inheritdocElement.Attribute("cref");
                    if (crefAttr?.Value != null && members.TryGetValue(crefAttr.Value, out var realDocMember))
                    {
                        var parentElement = inheritdocElement.Parent;
                        if (parentElement != null)
                        {
                            // Find the corresponding element in the referenced member with the same tag name
                            var correspondingElement = realDocMember.Element(parentElement.Name);
                            if (correspondingElement != null)
                            {
                                // Replace with the inner content of the corresponding element
                                parentElement.ReplaceNodes(correspondingElement.Nodes());
                            }
                            else
                            {
                                // If no corresponding element found, remove the inheritdoc element
                                inheritdocElement.Remove();
                            }
                        }
                    }
                }
            }

            options.IncludeXmlComments(() => new XPathDocument(xmlDoc.CreateReader()), includeControllerXmlComments);
        }
    }
}