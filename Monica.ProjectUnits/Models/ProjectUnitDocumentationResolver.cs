using System.Reflection;
using Monica.Core.XmlDocumentation.Abstractions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Resolves project-unit method metadata and optional XML documentation for one host.
/// </summary>
internal sealed class ProjectUnitDocumentationResolver(IXmlDocumentationService? xmlDocumentationService)
{
    internal List<ProjectUnitMethod> GetPublicMethods(Type type, Type? baseType = null)
    {
        return GetMethods(type, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static, baseType);
    }

    internal string? ExtractTypeDescription(Type type)
    {
        return xmlDocumentationService?.GetTypeDocumentation(type);
    }

    private List<ProjectUnitMethod> GetMethods(Type type, BindingFlags bindingFlags, Type? baseType)
    {
        var methods = type.GetMethods(bindingFlags)
            .Where(method => !method.IsSpecialName && method.DeclaringType != typeof(object));

        if (baseType is not null)
        {
            methods = methods.Where(method => method.DeclaringType != baseType);
        }

        return
        [
            .. methods.Select(method => new ProjectUnitMethod
            {
                MethodInfo = method,
                Description = xmlDocumentationService?.GetMethodDocumentation(method)?.Summary
            })
        ];
    }
}
