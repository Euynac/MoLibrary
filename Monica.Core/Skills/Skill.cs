using System.Collections.Frozen;
using System.Text.Json;
using Monica.Core.Skills.Models;

namespace Monica.Core.Skills;

/// <summary>
/// Base class for Monica skills that can be adapted to AI runtimes by an enabled skill-system module.
/// </summary>
/// <remarks>
/// A skill definition is inert by itself. The Monica AI skill system discovers concrete skills during module
/// startup and adapts them to the active agent framework only when that module is enabled.
/// </remarks>
public abstract class Skill
{
    /// <summary>
    /// Initializes the runtime skill base. Direct inheritance is restricted; use <see cref="Skill{TSelf}" /> for
    /// Monica skill authoring.
    /// </summary>
    private protected Skill()
    {
    }

    /// <summary>
    /// Gets the framework-neutral skill definition.
    /// </summary>
    public abstract SkillDefinition Definition { get; }

    /// <summary>
    /// Module strategy types that must be loaded for this skill to be available.
    /// </summary>
    public virtual IReadOnlySet<Type> RequiredModules => FrozenSet<Type>.Empty;

    /// <summary>
    /// Determines whether this skill should be included in the startup skill catalog.
    /// </summary>
    public virtual bool IsEnabled => true;

    /// <summary>
    /// Gets a stable, UI-localizable reason code explaining why <see cref="IsEnabled"/> is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Return <see langword="null"/> when the skill is enabled. Disabled skills should prefer stable message codes over
    /// user-facing prose so management UIs can localize the reason.
    /// </remarks>
    public virtual string? DisabledReason => null;

    /// <summary>
    /// Gets optional metadata that allows this skill to be exposed as a local MCP server.
    /// </summary>
    /// <remarks>
    /// Return <see langword="null" /> for normal in-process skills. When a definition is provided, Monica can
    /// adapt the skill's <c>SkillToolAttribute</c> methods into MCP tools and list the skill-backed MCP server in
    /// capability management UIs. Runtime changes to this exposure are persisted, but the MCP endpoint is usually
    /// materialized at startup, so enabling or disabling exposure may require restarting the host.
    /// </remarks>
    public virtual SkillMcpServerDefinition? McpServerDefinition => null;

    /// <summary>
    /// Gets serializer options used by adapters to marshal skill scripts and dynamic resources.
    /// </summary>
    public virtual JsonSerializerOptions? SerializerOptions => null;
}
