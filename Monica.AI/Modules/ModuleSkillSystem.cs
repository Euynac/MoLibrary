using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.AI.Skills.Models;
using Monica.AI.Skills.Services;
using Monica.AI.Services.Support.ModuleCatalog;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.Skills;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Extension methods for configuring Monica's AI skill system.
/// </summary>
public static class ModuleSkillSystemBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Enables class-based AI skill discovery and registration.
        /// </summary>
        /// <param name="action">Optional configuration action.</param>
        /// <returns>The skill-system module guide.</returns>
        public static ModuleSkillSystemGuide AddAISkillSystem(Action<ModuleSkillSystemOption>? action = null)
        {
            return new ModuleSkillSystemGuide().Register(action);
        }
    }
}

/// <summary>
/// Discovers Monica skill classes and exposes them through Microsoft Agent Skills.
/// </summary>
[ModuleKey(BuiltInModuleKey.AISkillSystem)]
public sealed class ModuleSkillSystem(ModuleSkillSystemOption option)
    : ModuleBase<ModuleSkillSystem, ModuleSkillSystemOption, ModuleSkillSystemGuide>(option),
      IBusinessTypeIterator
{
    private readonly List<Type> _skillTypes = [];

    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleXmlDocumentationGuide>().Register();
    }

    /// <inheritdoc />
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false })
            {
                if (type.IsAssignableTo(typeof(Skill)))
                {
                    _skillTypes.Add(type);
                }
            }

            yield return type;
        }
    }

    /// <inheritdoc />
    public override void PostConfigureServices(IServiceCollection services)
    {
        foreach (var skillType in _skillTypes.Distinct())
        {
            if (!services.Any(descriptor => descriptor.ServiceType == skillType))
            {
                services.AddSingleton(skillType);
            }

            services.AddSingleton(typeof(Skill), sp => (Skill)sp.GetRequiredService(skillType));
        }

        services.TryAddSingleton<ILoadedModuleCatalog, ModuleRegistryLoadedModuleCatalog>();
        services.TryAddSingleton<MonicaSkillCatalog>();
        services.TryAddSingleton<MonicaAgentSkillsProviderFactory>();

        foreach (var registration in option.ExternalFileSkillRegistrations)
        {
            services.AddSingleton(registration);
        }
    }
}

/// <summary>
/// Configuration options for the AI skill system.
/// </summary>
public sealed class ModuleSkillSystemOption : ModuleOptions<ModuleSkillSystem>
{
    internal List<ExternalFileSkillRegistration> ExternalFileSkillRegistrations { get; } = [];

    /// <summary>
    /// Adds one or more file-based Agent Framework skill roots.
    /// </summary>
    /// <param name="skillPaths">
    /// Filesystem roots to scan. Each root may be a single skill directory containing <c>SKILL.md</c> or a package
    /// directory containing multiple skill directories; Agent Framework searches recursively up to two levels deep.
    /// </param>
    /// <param name="scriptRunner">
    /// Optional script runner used when discovered skills contain scripts. Leave <see langword="null"/> only when scripts
    /// should remain discoverable but fail if invoked.
    /// </param>
    /// <param name="options">Optional Agent Framework file-skill discovery options.</param>
    /// <param name="usesSubprocessRunner">Whether the supplied runner is Monica's built-in subprocess runner.</param>
    public void AddFileSkills(
        IEnumerable<string> skillPaths,
        AgentFileSkillScriptRunner? scriptRunner = null,
        AgentFileSkillsSourceOptions? options = null,
        bool usesSubprocessRunner = false)
    {
        ArgumentNullException.ThrowIfNull(skillPaths);

        var normalizedPaths = skillPaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedPaths.Count == 0)
        {
            throw new ArgumentException("At least one skill path is required.", nameof(skillPaths));
        }

        ExternalFileSkillRegistrations.Add(
            new ExternalFileSkillRegistration(normalizedPaths, scriptRunner, options, usesSubprocessRunner));
    }
}

/// <summary>
/// Configuration guide for the AI skill system.
/// </summary>
public sealed class ModuleSkillSystemGuide
    : ModuleGuide<ModuleSkillSystem, ModuleSkillSystemOption, ModuleSkillSystemGuide>
{
    /// <summary>
    /// Registers file-based Agent Framework skills from a single filesystem root.
    /// </summary>
    /// <param name="skillPath">
    /// Root to scan. It may point directly at one skill directory containing <c>SKILL.md</c> or at a package directory
    /// containing multiple skill directories.
    /// </param>
    /// <param name="scriptRunner">Optional runner for file-based scripts.</param>
    /// <param name="options">Optional discovery options for resource directories, script directories, and allowed extensions.</param>
    /// <returns>The current guide instance.</returns>
    public ModuleSkillSystemGuide AddFileSkills(
        string skillPath,
        AgentFileSkillScriptRunner? scriptRunner = null,
        AgentFileSkillsSourceOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillPath);
        return AddFileSkills([skillPath], scriptRunner, options);
    }

    /// <summary>
    /// Registers file-based Agent Framework skills from one or more filesystem roots.
    /// </summary>
    /// <param name="skillPaths">
    /// Roots to scan. Each path may point directly at one skill directory containing <c>SKILL.md</c> or at a package
    /// directory containing multiple skill directories. Agent Framework searches recursively up to two levels deep.
    /// </param>
    /// <param name="scriptRunner">Optional runner for file-based scripts.</param>
    /// <param name="options">Optional discovery options for resource directories, script directories, and allowed extensions.</param>
    /// <returns>The current guide instance.</returns>
    public ModuleSkillSystemGuide AddFileSkills(
        IEnumerable<string> skillPaths,
        AgentFileSkillScriptRunner? scriptRunner = null,
        AgentFileSkillsSourceOptions? options = null)
    {
        return AddFileSkillsCore(skillPaths, scriptRunner, options, usesSubprocessRunner: false);
    }

    /// <summary>
    /// Registers file-based Agent Framework skills and uses Monica's local subprocess runner for skill scripts.
    /// </summary>
    /// <param name="skillPath">
    /// Root to scan. It may point directly at one skill directory containing <c>SKILL.md</c> or at a package directory
    /// containing multiple skill directories.
    /// </param>
    /// <param name="options">Optional discovery options for resource directories, script directories, and allowed extensions.</param>
    /// <returns>The current guide instance.</returns>
    public ModuleSkillSystemGuide AddFileSkillsWithSubprocessRunner(
        string skillPath,
        AgentFileSkillsSourceOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillPath);
        return AddFileSkillsCore([skillPath], MonicaSubprocessSkillScriptRunner.RunAsync, options, usesSubprocessRunner: true);
    }

    /// <summary>
    /// Registers file-based Agent Framework skills and uses Monica's local subprocess runner for skill scripts.
    /// </summary>
    /// <param name="skillPaths">
    /// Roots to scan. Each path may point directly at one skill directory containing <c>SKILL.md</c> or at a package
    /// directory containing multiple skill directories. Agent Framework searches recursively up to two levels deep.
    /// </param>
    /// <param name="options">Optional discovery options for resource directories, script directories, and allowed extensions.</param>
    /// <returns>The current guide instance.</returns>
    public ModuleSkillSystemGuide AddFileSkillsWithSubprocessRunner(
        IEnumerable<string> skillPaths,
        AgentFileSkillsSourceOptions? options = null)
    {
        return AddFileSkillsCore(
            skillPaths,
            MonicaSubprocessSkillScriptRunner.RunAsync,
            options,
            usesSubprocessRunner: true);
    }

    private ModuleSkillSystemGuide AddFileSkillsCore(
        IEnumerable<string> skillPaths,
        AgentFileSkillScriptRunner? scriptRunner,
        AgentFileSkillsSourceOptions? options,
        bool usesSubprocessRunner)
    {
        ArgumentNullException.ThrowIfNull(skillPaths);
        var normalizedPaths = skillPaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path.Trim())
            .ToList();

        if (normalizedPaths.Count == 0)
        {
            throw new ArgumentException("At least one skill path is required.", nameof(skillPaths));
        }

        ConfigureModuleOption(
            option => option.AddFileSkills(normalizedPaths, scriptRunner, options, usesSubprocessRunner),
            secondKey: string.Join("|", normalizedPaths));
        return this;
    }
}
