using System.Security;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monica.Core.Skills;
using Monica.Core.Skills.Models;
using Monica.Core.XmlDocumentation.Abstractions;

namespace Monica.AI.Skills.Internal;

internal sealed class MonicaAgentSkillAdapter : AgentSkill
{
    private readonly Skill _skill;
    private readonly Lazy<AgentSkillFrontmatter> _frontmatter;
    private readonly Lazy<IReadOnlyList<AgentSkillScript>?> _scripts;
    private readonly Lazy<IReadOnlyList<AgentSkillResource>?> _resources;
    private readonly Lazy<string> _content;

    internal MonicaAgentSkillAdapter(
        Skill skill,
        IXmlDocumentationService xmlDocumentationService)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(xmlDocumentationService);

        _skill = skill;
        _frontmatter = new Lazy<AgentSkillFrontmatter>(() => CreateFrontmatter(_skill.Definition));
        _scripts = new Lazy<IReadOnlyList<AgentSkillScript>?>(
            () => SkillScriptDiscovery.Discover(_skill, xmlDocumentationService));
        _resources = new Lazy<IReadOnlyList<AgentSkillResource>?>(
            () => SkillResourceDiscovery.Discover(_skill, xmlDocumentationService));
        _content = new Lazy<string>(() => BuildContent(_skill.Definition, _resources.Value, _scripts.Value));
    }

    /// <summary>
    /// Gets the wrapped Monica skill instance.
    /// </summary>
    internal Skill Skill => _skill;

    /// <inheritdoc />
    public override AgentSkillFrontmatter Frontmatter => _frontmatter.Value;

    /// <inheritdoc />
    public override string Content => _content.Value;

    /// <inheritdoc />
    public override IReadOnlyList<AgentSkillResource>? Resources => _resources.Value;

    /// <inheritdoc />
    public override IReadOnlyList<AgentSkillScript>? Scripts => _scripts.Value;

    private static AgentSkillFrontmatter CreateFrontmatter(SkillDefinition definition)
    {
        var frontmatter = new AgentSkillFrontmatter(
            definition.Name,
            definition.Description,
            definition.Compatibility)
        {
            License = definition.License,
            AllowedTools = definition.AllowedTools
        };

        if (definition.Metadata is not null)
        {
            frontmatter.Metadata = new AdditionalPropertiesDictionary();
            foreach (var (key, value) in definition.Metadata)
            {
                frontmatter.Metadata[key] = value;
            }
        }

        return frontmatter;
    }

    private static string BuildContent(
        SkillDefinition definition,
        IReadOnlyList<AgentSkillResource>? resources,
        IReadOnlyList<AgentSkillScript>? scripts)
    {
        var builder = new StringBuilder();
        builder.Append("<name>")
            .Append(Escape(definition.Name))
            .AppendLine("</name>")
            .Append("<description>")
            .Append(Escape(definition.Description))
            .AppendLine("</description>")
            .AppendLine()
            .AppendLine("<instructions>")
            .AppendLine(Escape(definition.Instructions))
            .Append("</instructions>");

        if (resources is { Count: > 0 })
        {
            builder.AppendLine()
                .AppendLine()
                .AppendLine("<resources>");

            foreach (var resource in resources)
            {
                builder.Append("  <resource name=\"")
                    .Append(Escape(resource.Name))
                    .Append('"');

                if (!string.IsNullOrWhiteSpace(resource.Description))
                {
                    builder.Append(" description=\"")
                        .Append(Escape(resource.Description))
                        .Append('"');
                }

                builder.AppendLine("/>");
            }

            builder.Append("</resources>");
        }

        if (scripts is { Count: > 0 })
        {
            builder.AppendLine()
                .AppendLine()
                .AppendLine("<scripts>");

            foreach (var script in scripts)
            {
                var parametersSchema = script.ParametersSchema;

                builder.Append("  <script name=\"")
                    .Append(Escape(script.Name))
                    .Append('"');

                if (!string.IsNullOrWhiteSpace(script.Description))
                {
                    builder.Append(" description=\"")
                        .Append(Escape(script.Description))
                        .Append('"');
                }

                if (parametersSchema is null)
                {
                    builder.AppendLine("/>");
                    continue;
                }

                builder.AppendLine(">")
                    .Append("    <parameters_schema>")
                    .Append(Escape(parametersSchema.Value.GetRawText()))
                    .AppendLine("</parameters_schema>")
                    .AppendLine("  </script>");
            }

            builder.Append("</scripts>");
        }

        return builder.ToString();
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
