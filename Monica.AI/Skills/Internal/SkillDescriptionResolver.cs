using System.ComponentModel;
using System.Reflection;
using Monica.Core.Skills.Annotations;
using Monica.Core.XmlDocumentation.Abstractions;

namespace Monica.AI.Skills.Internal;

internal static class SkillDescriptionResolver
{
    internal static string ResolveMethod(MethodInfo method, IXmlDocumentationService? xmlDocs)
    {
        ArgumentNullException.ThrowIfNull(method);

        var fromAttribute = method.GetCustomAttribute<SkillToolAttribute>()?.Description;
        if (!string.IsNullOrWhiteSpace(fromAttribute))
        {
            return fromAttribute;
        }

        var fromDescription = method.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (!string.IsNullOrWhiteSpace(fromDescription))
        {
            return fromDescription;
        }

        var fromXml = xmlDocs?.GetMethodDocumentation(method)?.Summary;
        if (!string.IsNullOrWhiteSpace(fromXml))
        {
            return fromXml;
        }

        return $"{method.DeclaringType?.Name}.{method.Name}";
    }

    internal static string ResolveParameter(ParameterInfo parameter, IXmlDocumentationService? xmlDocs)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        var fromAttribute = parameter.GetCustomAttribute<SkillToolAttribute>()?.Description;
        if (!string.IsNullOrWhiteSpace(fromAttribute))
        {
            return fromAttribute;
        }

        var fromDescription = parameter.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (!string.IsNullOrWhiteSpace(fromDescription))
        {
            return fromDescription;
        }

        if (xmlDocs is null || parameter.Member is not MethodInfo method || parameter.Name is not { } name)
        {
            return string.Empty;
        }

        var methodDocs = xmlDocs.GetMethodDocumentation(method);
        return methodDocs?.Parameters.TryGetValue(name, out var fromXml) == true
               && !string.IsNullOrWhiteSpace(fromXml)
            ? fromXml
            : string.Empty;
    }

    internal static string? ResolveResource(MemberInfo member, IXmlDocumentationService? xmlDocs)
    {
        ArgumentNullException.ThrowIfNull(member);

        var fromAttribute = member.GetCustomAttribute<SkillResourceAttribute>()?.Description;
        if (!string.IsNullOrWhiteSpace(fromAttribute))
        {
            return fromAttribute;
        }

        var fromDescription = member.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (!string.IsNullOrWhiteSpace(fromDescription))
        {
            return fromDescription;
        }

        return member is MethodInfo method
            ? xmlDocs?.GetMethodDocumentation(method)?.Summary
            : null;
    }
}
