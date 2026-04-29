using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monica.AI.Skills.Abstractions;
using Monica.AI.Skills.Services;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

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
/// Discovers Monica AI skill classes and exposes them through Microsoft Agent Skills.
/// </summary>
[ModuleKey(BuiltInModuleKey.AISkillSystem)]
public sealed class ModuleSkillSystem(ModuleSkillSystemOption option)
    : ModuleBase<ModuleSkillSystem, ModuleSkillSystemOption, ModuleSkillSystemGuide>(option),
      IBusinessTypeIterator
{
    private readonly List<Type> _skillTypes = [];
    private readonly List<Type> _toolTypes = [];
    private readonly List<Type> _mcpTypes = [];

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
                if (IsAssignableToOpenGeneric(type, typeof(MoSkill<>)))
                {
                    _skillTypes.Add(type);
                }
                else if (type.IsAssignableTo(typeof(MoTool)))
                {
                    _toolTypes.Add(type);
                }
                else if (type.IsAssignableTo(typeof(MoMcp)))
                {
                    _mcpTypes.Add(type);
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

            services.AddSingleton(typeof(AgentSkill), sp => sp.GetRequiredService(skillType));
        }

        services.TryAddSingleton<ILoadedModuleCatalog, ModuleRegistryLoadedModuleCatalog>();
        services.TryAddSingleton<MonicaSkillCatalog>();
        services.TryAddSingleton<MonicaAgentSkillsProviderFactory>();
        services.TryAddSingleton(sp => sp.GetRequiredService<MonicaAgentSkillsProviderFactory>().GetProvider());

        if (_toolTypes.Count > 0 || _mcpTypes.Count > 0)
        {
            Logger.LogWarning(
                "MoTool and MoMcp discovery is reserved but not active yet. Tool types: {ToolCount}; MCP types: {McpCount}.",
                _toolTypes.Count,
                _mcpTypes.Count);
        }
    }

    private static bool IsAssignableToOpenGeneric(Type type, Type openGenericType)
    {
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == openGenericType)
            {
                return true;
            }
        }

        return false;
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
