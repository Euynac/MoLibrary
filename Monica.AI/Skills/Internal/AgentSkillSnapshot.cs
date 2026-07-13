using System.Xml.Linq;
using Microsoft.Agents.AI;

namespace Monica.AI.Skills.Internal;

internal sealed record AgentSkillSnapshot(
    string Content,
    IReadOnlyList<AgentSkillScript> Scripts,
    IReadOnlyList<AgentSkillResource> Resources)
{
    internal static AgentSkillSnapshot Create(AgentSkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var content = skill.GetContentAsync().AsTask().GetAwaiter().GetResult();
        if (skill is MonicaAgentSkillAdapter codeSkill)
        {
            return new AgentSkillSnapshot(
                content,
                codeSkill.Scripts ?? [],
                codeSkill.Resources ?? []);
        }

        return new AgentSkillSnapshot(
            content,
            ResolveScripts(skill, content),
            ResolveResources(skill, content));
    }

    private static IReadOnlyList<AgentSkillScript> ResolveScripts(AgentSkill skill, string content)
    {
        return ReadManifestEntries(content, "available_scripts", "script")
            .Select(element => element.Attribute("name")?.Value)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(name => skill.GetScriptAsync(name!).AsTask().GetAwaiter().GetResult())
            .OfType<AgentSkillScript>()
            .ToList();
    }

    private static IReadOnlyList<AgentSkillResource> ResolveResources(AgentSkill skill, string content)
    {
        return ReadManifestEntries(content, "available_resources", "resource")
            .Select(element => element.Attribute("name")?.Value)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(name => skill.GetResourceAsync(name!).AsTask().GetAwaiter().GetResult())
            .OfType<AgentSkillResource>()
            .ToList();
    }

    private static IReadOnlyList<XElement> ReadManifestEntries(
        string content,
        string containerName,
        string entryName)
    {
        var openingTag = $"<{containerName}";
        var start = content.LastIndexOf(openingTag, StringComparison.Ordinal);
        if (start < 0)
        {
            return [];
        }

        var openingTagEnd = content.IndexOf('>', start);
        if (openingTagEnd < 0 || content[openingTagEnd - 1] == '/')
        {
            return [];
        }

        var closingTag = $"</{containerName}>";
        var end = content.IndexOf(closingTag, openingTagEnd, StringComparison.Ordinal);
        if (end < 0)
        {
            return [];
        }

        var manifest = content[start..(end + closingTag.Length)];
        return XElement.Parse(manifest)
            .Elements(entryName)
            .ToList();
    }
}
