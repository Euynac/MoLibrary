using System.Reflection;
using Microsoft.Agents.AI;
using Monica.AI.Skills.Abstractions;
using Monica.AI.Skills.Annotations;
using Monica.Core.XmlDocumentation.Abstractions;

namespace Monica.AI.Skills.Internal;

internal static class SkillScriptDiscovery
{
    private const BindingFlags DISCOVERY_FLAGS =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    internal static IReadOnlyList<AgentSkillScript>? Discover<TSelf>(
        Skill<TSelf> skill,
        IXmlDocumentationService? xmlDocs)
        where TSelf : Skill<TSelf>
    {
        ArgumentNullException.ThrowIfNull(skill);

        List<AgentSkillScript>? scripts = null;
        var scriptMethods = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

        foreach (var method in typeof(TSelf).GetMethods(DISCOVERY_FLAGS))
        {
            var toolAttribute = method.GetCustomAttribute<SkillToolAttribute>();
            if (toolAttribute is null || toolAttribute.Disabled)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(toolAttribute.Name)
                ? SkillToolNameHelper.DeriveName(method.Name)
                : toolAttribute.Name.Trim();

            if (scriptMethods.TryGetValue(name, out var existingMethod))
            {
                throw new InvalidOperationException(
                    $"Skill '{skill.Frontmatter.Name}' exposes duplicate script name '{name}' from methods " +
                    $"'{existingMethod.Name}' and '{method.Name}'. Set an explicit [SkillTool(Name = ...)] value.");
            }

            scriptMethods.Add(name, method);
            scripts ??= [];
            scripts.Add(new InlineSkillScript(
                name,
                method,
                method.IsStatic ? null : skill,
                SkillDescriptionResolver.ResolveMethod(method, xmlDocs),
                skill.ScriptSerializerOptions,
                xmlDocs));
        }

        return scripts;
    }
}
