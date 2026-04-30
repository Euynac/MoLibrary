using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Monica.AI.Services.Support.ModuleCatalog;
using Monica.AI.Skills.Internal;
using Monica.Core.Modularity.Models;
using Monica.Core.Skills;
using Monica.Core.XmlDocumentation.Abstractions;

namespace Monica.AI.Skills.Services;

/// <summary>
/// Builds the static skill catalog exposed to chat agents.
/// </summary>
public sealed class MonicaSkillCatalog(
    IEnumerable<Skill> skills,
    ILoadedModuleCatalog loadedModules,
    IXmlDocumentationService xmlDocumentationService,
    ILogger<MonicaSkillCatalog> logger)
{
    private readonly Lazy<IReadOnlyList<AgentSkill>> _activeSkills = new(() =>
    {
        var loadedModuleKeys = loadedModules.GetLoadedModuleKeys();
        var activeSkills = skills
            .Where(skill => IsActive(skill, loadedModuleKeys, logger))
            .OrderBy(skill => skill.Definition.Name, StringComparer.Ordinal)
            .ToList();

        ValidateUniqueSkillNames(activeSkills);

        return activeSkills
            .Select(skill => new MonicaAgentSkillAdapter(skill, xmlDocumentationService))
            .ToList();
    });

    /// <summary>
    /// Gets the filtered, process-static skill set.
    /// </summary>
    public IReadOnlyList<AgentSkill> GetActiveSkills() => _activeSkills.Value;

    private static bool IsActive(
        Skill skill,
        IReadOnlySet<ModuleKey> loadedModuleKeys,
        ILogger logger)
    {
        if (!skill.IsEnabled)
        {
            logger.LogDebug("Skipping disabled AI skill '{SkillName}'.", skill.Definition.Name);
            return false;
        }

        var missing = skill.RequiredModules
            .Where(required => !loadedModuleKeys.Contains(required))
            .ToList();

        if (missing.Count == 0)
        {
            return true;
        }

        logger.LogDebug(
            "Skipping AI skill '{SkillName}' because required modules are not loaded: {RequiredModules}.",
            skill.Definition.Name,
            string.Join(", ", missing));
        return false;
    }

    private static void ValidateUniqueSkillNames(IReadOnlyList<Skill> activeSkills)
    {
        var duplicateNames = activeSkills
            .GroupBy(skill => skill.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateNames.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Duplicate AI skill names are not allowed: " +
            string.Join(", ", duplicateNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)) + ".");
    }
}
