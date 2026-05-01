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
    public override string Content { get; } = content;

    /// <inheritdoc />
    public override IReadOnlyList<AgentSkillResource> Resources { get; } = resources;

    /// <inheritdoc />
    public override IReadOnlyList<AgentSkillScript> Scripts { get; } = scripts;
}
