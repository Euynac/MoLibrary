using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monica.Core.Skills;
using Monica.Core.Skills.Models;
using Monica.Core.XmlDocumentation.Abstractions;

namespace Monica.AI.Skills.Internal;

internal sealed class MonicaAgentSkillAdapter : AgentClassSkill<MonicaAgentSkillAdapter>
{
    private readonly Skill _skill;
    private readonly Lazy<AgentSkillFrontmatter> _frontmatter;
    private readonly Lazy<IReadOnlyList<AgentSkillScript>?> _scripts;
    private readonly Lazy<IReadOnlyList<AgentSkillResource>?> _resources;

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
    }

    /// <summary>
    /// Gets the wrapped Monica skill instance.
    /// </summary>
    internal Skill Skill => _skill;

    /// <inheritdoc />
    public override AgentSkillFrontmatter Frontmatter => _frontmatter.Value;

    /// <inheritdoc />
    public override IReadOnlyList<AgentSkillResource>? Resources => _resources.Value;

    /// <inheritdoc />
    public override IReadOnlyList<AgentSkillScript>? Scripts => _scripts.Value;

    /// <inheritdoc />
    protected override string Instructions => _skill.Definition.Instructions;

    /// <inheritdoc />
    protected override JsonSerializerOptions? SerializerOptions => _skill.SerializerOptions;

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
}
