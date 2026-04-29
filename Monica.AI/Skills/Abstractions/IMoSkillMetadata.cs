using Monica.Core.Modularity.Models;

namespace Monica.AI.Skills.Abstractions;

/// <summary>
/// Runtime metadata shared by all Monica skill implementations.
/// </summary>
public interface IMoSkillMetadata
{
    /// <summary>
    /// Module keys that must be loaded for the skill to be available.
    /// </summary>
    IEnumerable<ModuleKey> RequiredModules { get; }

    /// <summary>
    /// Determines whether this skill should be included in the startup skill catalog.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Ordering hint used when listing skills. Higher priority comes first.
    /// </summary>
    int Priority { get; }
}
