using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    }
}

/// <summary>
/// Configuration options for the AI skill system.
/// </summary>
public sealed class ModuleSkillSystemOption : ModuleOptions<ModuleSkillSystem>
{
}

/// <summary>
/// Configuration guide for the AI skill system.
/// </summary>
public sealed class ModuleSkillSystemGuide
    : ModuleGuide<ModuleSkillSystem, ModuleSkillSystemOption, ModuleSkillSystemGuide>
{
}
