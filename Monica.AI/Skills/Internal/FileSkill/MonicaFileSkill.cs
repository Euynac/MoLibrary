using System.Security;
using System.Text;
using Microsoft.Agents.AI;

namespace Monica.AI.Skills.Internal.FileSkill;

internal sealed class MonicaFileSkill(
    AgentSkillFrontmatter frontmatter,
    string content,
    string path,
    IReadOnlyList<AgentSkillResource> resources,
    IReadOnlyList<AgentSkillScript> scripts)
    : AgentSkill
{
    internal string Path { get; } = path;

    /// <inheritdoc />
    public override AgentSkillFrontmatter Frontmatter { get; } = frontmatter;

    /// <inheritdoc />
    public override string Content { get; } = AppendResourceManifest(content, resources);

    /// <inheritdoc />
    public override IReadOnlyList<AgentSkillResource> Resources { get; } = resources;

    /// <inheritdoc />
    public override IReadOnlyList<AgentSkillScript> Scripts { get; } = scripts;

    private static string AppendResourceManifest(string content, IReadOnlyList<AgentSkillResource> resources)
    {
        if (resources.Count == 0)
        {
            return content;
        }

        var canonicalResources = resources
            .OfType<MonicaFileSkillResource>()
            .Where(resource => string.Equals(resource.Name, resource.CanonicalName, StringComparison.Ordinal))
            .ToList();

        if (canonicalResources.Count == 0)
        {
            return content;
        }

        var sb = new StringBuilder(content.TrimEnd());
        sb.AppendLine()
            .AppendLine()
            .AppendLine("<resources>");

        foreach (var resource in canonicalResources.OrderBy(static resource => resource.Name, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append("  <resource name=\"")
                .Append(SecurityElement.Escape(resource.Name))
                .Append("\"");

            if (!string.IsNullOrWhiteSpace(resource.Description))
            {
                sb.Append(" description=\"")
                    .Append(SecurityElement.Escape(resource.Description))
                    .Append("\"");
            }

            sb.AppendLine(" />");
        }

        sb.AppendLine("</resources>")
            .Append("When using `read_skill_resource`, pass one of the resource `name` values exactly.");

        return sb.ToString();
    }
}
