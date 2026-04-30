using System.Reflection;
using Microsoft.Agents.AI;
using Monica.Core.Skills;
using Monica.Core.Skills.Annotations;
using Monica.Core.XmlDocumentation.Abstractions;

namespace Monica.AI.Skills.Internal;

internal static class SkillResourceDiscovery
{
    private const BindingFlags DISCOVERY_FLAGS =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    internal static IReadOnlyList<AgentSkillResource>? Discover(
        Skill skill,
        IXmlDocumentationService xmlDocs)
    {
        ArgumentNullException.ThrowIfNull(skill);

        List<AgentSkillResource>? resources = null;
        var resourceMembers = new Dictionary<string, MemberInfo>(StringComparer.Ordinal);
        var discoveryType = skill.GetType();

        foreach (var property in discoveryType.GetProperties(DISCOVERY_FLAGS))
        {
            var resourceAttribute = property.GetCustomAttribute<SkillResourceAttribute>();
            if (resourceAttribute is null || resourceAttribute.Disabled)
            {
                continue;
            }

            var getter = property.GetGetMethod(nonPublic: true);
            if (getter is null)
            {
                continue;
            }

            if (getter.GetParameters().Length > 0)
            {
                throw new InvalidOperationException(
                    $"Property '{property.Name}' on skill '{skill.Definition.Name}' is an indexer and cannot be used as a skill resource.");
            }

            var name = ResolveName(resourceAttribute.Name, property.Name);
            EnsureUniqueName(skill, resourceMembers, name, property);

            resources ??= [];
            resources.Add(new InlineSkillResource(
                name,
                getter,
                getter.IsStatic ? null : skill,
                SkillDescriptionResolver.ResolveResource(property, xmlDocs),
                skill.SerializerOptions));
        }

        foreach (var method in discoveryType.GetMethods(DISCOVERY_FLAGS))
        {
            var resourceAttribute = method.GetCustomAttribute<SkillResourceAttribute>();
            if (resourceAttribute is null || resourceAttribute.Disabled)
            {
                continue;
            }

            ValidateResourceMethodParameters(skill, method);

            var name = ResolveName(resourceAttribute.Name, method.Name);
            EnsureUniqueName(skill, resourceMembers, name, method);

            resources ??= [];
            resources.Add(new InlineSkillResource(
                name,
                method,
                method.IsStatic ? null : skill,
                SkillDescriptionResolver.ResolveResource(method, xmlDocs),
                skill.SerializerOptions));
        }

        return resources;
    }

    private static string ResolveName(string? configuredName, string memberName)
        => string.IsNullOrWhiteSpace(configuredName)
            ? SkillToolNameHelper.DeriveName(memberName)
            : configuredName.Trim();

    private static void EnsureUniqueName(
        Skill skill,
        Dictionary<string, MemberInfo> resourceMembers,
        string name,
        MemberInfo member)
    {
        if (!resourceMembers.TryGetValue(name, out var existingMember))
        {
            resourceMembers.Add(name, member);
            return;
        }

        throw new InvalidOperationException(
            $"Skill '{skill.Definition.Name}' exposes duplicate resource name '{name}' from members " +
            $"'{existingMember.Name}' and '{member.Name}'. Set an explicit [SkillResource(Name = ...)] value.");
    }

    private static void ValidateResourceMethodParameters(Skill skill, MethodInfo method)
    {
        foreach (var parameter in method.GetParameters())
        {
            if (parameter.ParameterType == typeof(IServiceProvider)
                || parameter.ParameterType == typeof(CancellationToken))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Method '{method.Name}' on skill '{skill.Definition.Name}' has parameter '{parameter.Name}' of type " +
                $"'{parameter.ParameterType}' which cannot be supplied when reading a resource. " +
                "Resource methods may only accept IServiceProvider and/or CancellationToken parameters.");
        }
    }
}
