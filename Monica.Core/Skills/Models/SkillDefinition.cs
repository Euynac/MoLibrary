namespace Monica.Core.Skills.Models;

/// <summary>
/// Describes a Monica skill independently of any concrete AI agent framework.
/// </summary>
/// <remarks>
/// The definition is the stable authoring contract used by Monica modules. AI integrations may adapt this
/// information into provider-specific frontmatter, manifests, prompts, or other metadata formats.
/// </remarks>
public sealed record SkillDefinition
{
    /// <summary>
    /// Creates a skill definition.
    /// </summary>
    /// <param name="name">
    /// Stable skill identifier. Use lowercase kebab-case so adapters can pass it through to providers with
    /// strict skill-name validation.
    /// </param>
    /// <param name="description">Short discovery description shown to AI agents before the full skill is loaded.</param>
    /// <param name="instructions">Full skill instructions used after the skill is selected or loaded.</param>
    public SkillDefinition(string name, string description, string instructions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);

        Name = name;
        Description = description;
        Instructions = instructions;
    }

    /// <summary>
    /// Stable skill identifier. Use lowercase kebab-case for compatibility with agent-skill runtimes.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Short discovery description shown to AI agents before the full skill is loaded.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Full skill instructions used after the skill is selected or loaded.
    /// </summary>
    public string Instructions { get; }

    /// <summary>
    /// Optional license name or reference for skill content and behavior.
    /// </summary>
    public string? License { get; init; }

    /// <summary>
    /// Optional compatibility note for agent runtimes, models, hosts, or module versions.
    /// </summary>
    public string? Compatibility { get; init; }

    /// <summary>
    /// Optional provider-specific allow-list of tools that the skill expects to use.
    /// </summary>
    public string? AllowedTools { get; init; }

    /// <summary>
    /// Optional arbitrary metadata for AI adapters. Values should be JSON-serializable primitives, arrays,
    /// or objects when possible.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
}
