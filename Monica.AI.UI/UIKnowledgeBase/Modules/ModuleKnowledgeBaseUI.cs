using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Knowledge-base UI module builder extensions.
/// </summary>
public static class ModuleKnowledgeBaseUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures knowledge-base UI components.
        /// </summary>
        /// <param name="action">Optional configuration action.</param>
        /// <returns>The knowledge-base UI module guide.</returns>
        public static ModuleKnowledgeBaseUIGuide AddKnowledgeBaseUI(Action<ModuleKnowledgeBaseUIOption>? action = null)
        {
            return new ModuleKnowledgeBaseUIGuide().Register(action);
        }
    }
}

/// <summary>
/// UI module for knowledge-base selection and management surfaces.
/// </summary>
[ModuleKey(BuiltInModuleKey.KnowledgeBaseUI)]
public sealed class ModuleKnowledgeBaseUI(ModuleKnowledgeBaseUIOption option)
    : ModuleBase<ModuleKnowledgeBaseUI, ModuleKnowledgeBaseUIOption, ModuleKnowledgeBaseUIGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleKnowledgeBaseGuide>().Register();
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        // Component-only module for now. Future KB management state belongs here.
    }
}

/// <summary>
/// Configuration options for the knowledge-base UI module.
/// </summary>
public sealed class ModuleKnowledgeBaseUIOption : ModuleOptions<ModuleKnowledgeBaseUI>
{
}

/// <summary>
/// Configuration guide for the knowledge-base UI module.
/// </summary>
public sealed class ModuleKnowledgeBaseUIGuide
    : ModuleGuide<ModuleKnowledgeBaseUI, ModuleKnowledgeBaseUIOption, ModuleKnowledgeBaseUIGuide>
{
}
