using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.AgentCapabilities.Services;
using Monica.AI.Services.Support.ModuleCatalog;
using Monica.AI.Skills.Internal.FileSkill;
using Monica.AI.Skills.Internal;
using Monica.AI.Skills.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.Skills;
using Monica.Core.XmlDocumentation.Abstractions;

namespace Monica.AI.Skills.Services;

/// <summary>
/// Builds the static skill catalog exposed to chat agents.
/// </summary>
public sealed class MonicaSkillCatalog(
    IEnumerable<Skill> skills,
    IEnumerable<ExternalFileSkillRegistration> externalFileSkillRegistrations,
    ILoadedModuleCatalog loadedModules,
    IXmlDocumentationService xmlDocumentationService,
    ILoggerFactory loggerFactory,
    ILogger<MonicaSkillCatalog> logger)
{
    private readonly Lazy<SkillCatalogSnapshot> _snapshot = new(() =>
    {
        var loadedModuleKeys = loadedModules.GetLoadedModuleKeys();
        var entries = skills
            .Select(skill => BuildCodeEntry(skill, loadedModuleKeys, xmlDocumentationService, logger))
            .ToList();

        var externalFileSnapshot = BuildExternalFileSnapshot(externalFileSkillRegistrations, loggerFactory, logger);
        entries.AddRange(externalFileSnapshot.Entries);

        ValidateUniqueSkillNames(entries);
        return new SkillCatalogSnapshot(
            entries
                .OrderBy(skill => skill.Definition.Name, StringComparer.Ordinal)
                .ToList(),
            externalFileSnapshot.SourceStatuses);
    });

    /// <summary>
    /// Gets the runtime-enabled skill set adapted for Microsoft Agent Skills.
    /// </summary>
    public IReadOnlyList<AgentSkill> GetActiveSkills(AgentCapabilityState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return _snapshot.Value.Entries
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

        return _snapshot.Value.Entries
            .Select(entry => entry.ToInfo(state))
            .ToList();
    }

    /// <summary>
    /// Gets discovery status for registered file-based skill sources.
    /// </summary>
    public IReadOnlyList<AgentCapabilityFileSkillSourceStatusInfo> GetFileSkillSourceStatuses()
    {
        return _snapshot.Value.FileSkillSourceStatuses;
    }

    private static SkillEntry BuildCodeEntry(
        Skill skill,
        IReadOnlySet<ModuleKey> loadedModuleKeys,
        IXmlDocumentationService xmlDocumentationService,
        ILogger logger)
    {
        if (!skill.IsEnabled)
        {
            logger.LogDebug("Skipping disabled AI skill '{SkillName}'.", skill.Definition.Name);
            return new SkillEntry(
                skill.Definition.Name,
                skill.Definition.Description,
                skill.Definition.Instructions,
                skill.GetType().FullName,
                skill.RequiredModules.Select(static module => module.Value).ToList(),
                skill.IsEnabled,
                new MonicaAgentSkillAdapter(skill, xmlDocumentationService),
                AgentCapabilitySourceKind.CodeDefined,
                null,
                "Disabled by the skill implementation.");
        }

        var missing = skill.RequiredModules
            .Where(required => !loadedModuleKeys.Contains(required))
            .ToList();

        if (missing.Count == 0)
        {
            return new SkillEntry(
                skill.Definition.Name,
                skill.Definition.Description,
                skill.Definition.Instructions,
                skill.GetType().FullName,
                skill.RequiredModules.Select(static module => module.Value).ToList(),
                skill.IsEnabled,
                new MonicaAgentSkillAdapter(skill, xmlDocumentationService),
                AgentCapabilitySourceKind.CodeDefined,
                null,
                null);
        }

        logger.LogDebug(
            "Skipping AI skill '{SkillName}' because required modules are not loaded: {RequiredModules}.",
            skill.Definition.Name,
            string.Join(", ", missing));
        return new SkillEntry(
            skill.Definition.Name,
            skill.Definition.Description,
            skill.Definition.Instructions,
            skill.GetType().FullName,
            skill.RequiredModules.Select(static module => module.Value).ToList(),
            skill.IsEnabled,
            new MonicaAgentSkillAdapter(skill, xmlDocumentationService),
            AgentCapabilitySourceKind.CodeDefined,
            null,
            "Required modules are not loaded: " + string.Join(", ", missing) + ".");
    }

    private static ExternalFileSkillCatalogSnapshot BuildExternalFileSnapshot(
        IEnumerable<ExternalFileSkillRegistration> registrations,
        ILoggerFactory loggerFactory,
        ILogger logger)
    {
        var entries = new List<SkillEntry>();
        var sourceStatuses = new List<AgentCapabilityFileSkillSourceStatusInfo>();

        foreach (var registration in registrations)
        {
            var source = new MonicaFileSkillsSource(
                registration.SkillPaths,
                registration.ScriptRunner,
                registration.Options,
                loggerFactory.CreateLogger<MonicaFileSkillsSource>());
            var discovery = source.Discover();
            sourceStatuses.Add(BuildSourceStatus(registration, discovery));

            foreach (var skill in discovery.Skills)
            {
                var sourcePath = skill is MonicaFileSkill fileSkill ? fileSkill.Path : null;
                logger.LogDebug("Loaded external file AI skill '{SkillName}' from '{SkillPath}'.", skill.Frontmatter.Name, sourcePath);
                entries.Add(new SkillEntry(
                    skill.Frontmatter.Name,
                    skill.Frontmatter.Description,
                    skill.Content,
                    skill.GetType().FullName,
                    [],
                    true,
                    skill,
                    AgentCapabilitySourceKind.ExternalFile,
                    sourcePath,
                    null));
            }
        }

        return new ExternalFileSkillCatalogSnapshot(entries, sourceStatuses);
    }

    private static AgentCapabilityFileSkillSourceStatusInfo BuildSourceStatus(
        ExternalFileSkillRegistration registration,
        MonicaFileSkillsDiscoveryResult discovery)
    {
        var loadedSkillNames = discovery.Skills
            .Select(static skill => skill.Frontmatter.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AgentCapabilityFileSkillSourceStatusInfo(
            registration.SkillPaths,
            registration.ScriptRunner is not null,
            registration.UsesSubprocessRunner,
            loadedSkillNames.Count,
            loadedSkillNames,
            discovery.Paths.Select(static path => new AgentCapabilityFileSkillPathStatusInfo(
                path.Path,
                path.Exists,
                path.LoadedSkillCount,
                path.LoadedSkillNames,
                path.Issues)).ToList(),
            discovery.Issues.Distinct(StringComparer.Ordinal).ToList());
    }

    private static void ValidateUniqueSkillNames(IReadOnlyList<SkillEntry> activeSkills)
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
        string Name,
        string Description,
        string? Content,
        string? ImplementationType,
        IReadOnlyList<string> RequiredModules,
        bool IsBuiltInEnabled,
        AgentSkill AgentSkill,
        AgentCapabilitySourceKind SourceKind,
        string? SourcePath,
        string? DiscoveryDisabledReason)
    {
        internal SkillEntryDefinition Definition { get; } = new(Name, Description);

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
                Content,
                ImplementationType,
                RequiredModules,
                IsBuiltInEnabled,
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
                    resource.Description)).ToList(),
                sourceKind: SourceKind,
                sourcePath: SourcePath);
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

    private sealed record SkillEntryDefinition(string Name, string Description);

    private sealed record SkillCatalogSnapshot(
        IReadOnlyList<SkillEntry> Entries,
        IReadOnlyList<AgentCapabilityFileSkillSourceStatusInfo> FileSkillSourceStatuses);

    private sealed record ExternalFileSkillCatalogSnapshot(
        IReadOnlyList<SkillEntry> Entries,
        IReadOnlyList<AgentCapabilityFileSkillSourceStatusInfo> SourceStatuses);
}
