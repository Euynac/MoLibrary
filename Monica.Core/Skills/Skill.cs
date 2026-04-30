using System.Text.Json;
using Monica.Core.Modularity.Models;
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
    /// Gets the framework-neutral skill definition.
    /// </summary>
    public abstract SkillDefinition Definition { get; }

    /// <summary>
    /// Module keys that must be loaded for this skill to be available.
    /// </summary>
    public virtual IEnumerable<ModuleKey> RequiredModules => [];

    /// <summary>
    /// Determines whether this skill should be included in the startup skill catalog.
    /// </summary>
    public virtual bool IsEnabled => true;

    /// <summary>
    /// Gets serializer options used by adapters to marshal skill scripts and dynamic resources.
    /// </summary>
    public virtual JsonSerializerOptions? SerializerOptions => null;
}
