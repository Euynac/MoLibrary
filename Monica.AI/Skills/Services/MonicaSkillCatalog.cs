using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Monica.AI.Skills.Abstractions;
using Monica.Core.Modularity.Models;

namespace Monica.AI.Skills.Services;

/// <summary>
/// Builds the static skill catalog exposed to chat agents.
/// </summary>
public sealed class MonicaSkillCatalog(
    IEnumerable<AgentSkill> skills,
    ILoadedModuleCatalog loadedModules,
    ILogger<MonicaSkillCatalog> logger)
{
    private readonly Lazy<IReadOnlyList<AgentSkill>> _activeSkills = new(() =>
    {
        var loadedModuleKeys = loadedModules.GetLoadedModuleKeys();
        return skills
            .Where(skill => IsActive(skill, loadedModuleKeys, logger))
            .OrderByDescending(GetPriority)
            .ThenBy(skill => skill.Frontmatter.Name, StringComparer.Ordinal)
            .ToList();
    });

    /// <summary>
    /// Gets the filtered, process-static skill set.
    /// </summary>
    public IReadOnlyList<AgentSkill> GetActiveSkills() => _activeSkills.Value;

    private static bool IsActive(
        AgentSkill skill,
        IReadOnlySet<ModuleKey> loadedModuleKeys,
        ILogger logger)
    {
        if (skill is not ISkillMetadata metadata)
        {
            return true;
        }

        if (!metadata.IsEnabled)
        {
            logger.LogDebug("Skipping disabled AI skill '{SkillName}'.", skill.Frontmatter.Name);
            return false;
        }

        var missing = metadata.RequiredModules
            .Where(required => !loadedModuleKeys.Contains(required))
            .ToList();

        if (missing.Count == 0)
        {
            return true;
        }

        logger.LogDebug(
            "Skipping AI skill '{SkillName}' because required modules are not loaded: {RequiredModules}.",
            skill.Frontmatter.Name,
            string.Join(", ", missing));
        return false;
    }

    private static int GetPriority(AgentSkill skill)
        => skill is ISkillMetadata metadata ? metadata.Priority : 0;
}
