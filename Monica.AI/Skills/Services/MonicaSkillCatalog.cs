using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.AgentCapabilities.Services;
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
    private readonly Lazy<IReadOnlyList<SkillEntry>> _entries = new(() =>
    {
        var loadedModuleKeys = loadedModules.GetLoadedModuleKeys();
        var entries = skills
            .Select(skill => BuildEntry(skill, loadedModuleKeys, xmlDocumentationService, logger))
            .OrderBy(skill => skill.Definition.Name, StringComparer.Ordinal)
            .ToList();

        ValidateUniqueSkillNames(entries.Select(static entry => entry.Skill).ToList());
        return entries;
    });

    /// <summary>
    /// Gets the runtime-enabled skill set adapted for Microsoft Agent Skills.
    /// </summary>
    public IReadOnlyList<AgentSkill> GetActiveSkills(AgentCapabilityState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return _entries.Value
            .Where(entry => entry.IsAvailable
                            && state.SkillsEnabled
                            && state.IsEntryEnabled(AgentCapabilityKind.Skill, entry.Definition.Name))
            .Select(entry => entry.AgentSkill)
            .ToList();
    }

    /// <summary>
    /// Gets management metadata for all discovered skills.
    /// </summary>
    public IReadOnlyList<AgentCapabilityEntryInfo> GetEntries(AgentCapabilityState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return _entries.Value
            .Select(entry => entry.ToInfo(state))
            .ToList();
    }

    private static SkillEntry BuildEntry(
        Skill skill,
        IReadOnlySet<ModuleKey> loadedModuleKeys,
        IXmlDocumentationService xmlDocumentationService,
        ILogger logger)
    {
        if (!skill.IsEnabled)
        {
            logger.LogDebug("Skipping disabled AI skill '{SkillName}'.", skill.Definition.Name);
            return new SkillEntry(
                skill,
                new MonicaAgentSkillAdapter(skill, xmlDocumentationService),
                "Disabled by the skill implementation.");
        }

        var missing = skill.RequiredModules
            .Where(required => !loadedModuleKeys.Contains(required))
            .ToList();

        if (missing.Count == 0)
        {
            return new SkillEntry(
                skill,
                new MonicaAgentSkillAdapter(skill, xmlDocumentationService),
                null);
        }

        logger.LogDebug(
            "Skipping AI skill '{SkillName}' because required modules are not loaded: {RequiredModules}.",
            skill.Definition.Name,
            string.Join(", ", missing));
        return new SkillEntry(
            skill,
            new MonicaAgentSkillAdapter(skill, xmlDocumentationService),
            "Required modules are not loaded: " + string.Join(", ", missing) + ".");
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

    private sealed record SkillEntry(
        Skill Skill,
        AgentSkill AgentSkill,
        string? DiscoveryDisabledReason)
    {
        internal Monica.Core.Skills.Models.SkillDefinition Definition => Skill.Definition;

        internal bool IsAvailable => DiscoveryDisabledReason is null;

        internal AgentCapabilityEntryInfo ToInfo(AgentCapabilityState state)
        {
            var scripts = AgentSkill.Scripts ?? [];
            var resources = AgentSkill.Resources ?? [];
            var catalogEnabled = state.SkillsEnabled;
            var entryEnabled = state.IsEntryEnabled(AgentCapabilityKind.Skill, Definition.Name);
            var disabledReason = ResolveDisabledReason(catalogEnabled, entryEnabled);

            return new AgentCapabilityEntryInfo(
                AgentCapabilityKind.Skill,
                Definition.Name,
                Definition.Name,
                Definition.Description,
                Definition.Instructions,
                Skill.GetType().FullName,
                Skill.RequiredModules.Select(static module => module.Value).ToList(),
                Skill.IsEnabled,
                catalogEnabled,
                entryEnabled,
                disabledReason,
                scripts.Select(script => new AgentCapabilityToolInfo(
                    script.Name,
                    script.Description,
                    IsAvailable && catalogEnabled && entryEnabled,
                    AgentCapabilitySchemaParser.FormatSchema(script.ParametersSchema),
                    AgentCapabilitySchemaParser.ParseParameters(script.ParametersSchema))).ToList(),
                resources.Select(resource => new AgentCapabilityResourceInfo(
                    resource.Name,
                    resource.Description)).ToList());
        }

        private string? ResolveDisabledReason(bool catalogEnabled, bool entryEnabled)
        {
            if (DiscoveryDisabledReason is not null)
            {
                return DiscoveryDisabledReason;
            }

            if (!catalogEnabled)
            {
                return "The skill catalog is globally disabled.";
            }

            return entryEnabled ? null : "This skill is disabled in runtime capability settings.";
        }
    }
}
