using Microsoft.Extensions.DependencyInjection;
using Monica.AI.UI.Localization;
using Monica.AI.UI.Pages;
using Monica.AI.UI.UIKnowledgeBase.State;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Knowledge-base UI module builder extensions.
/// </summary>
public static class ModuleKnowledgeBaseUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures knowledge-base UI components.
        /// </summary>
        /// <param name="action">Optional configuration action.</param>
        /// <returns>The knowledge-base UI module guide.</returns>
        public ModuleKnowledgeBaseUIGuide AddKnowledgeBaseUI(Action<ModuleKnowledgeBaseUIOption>? action = null)
        {
            return builder.AddModule<ModuleKnowledgeBaseUI, ModuleKnowledgeBaseUIOption, ModuleKnowledgeBaseUIGuide>(action);
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

        if (!Option.DisableKnowledgeBaseManagePage)
        {
            DependsOnModule<ModuleRAGGuide>().Register();
            DependsOnModule<ModuleLocalizationGuide>().Register()
                .AddResource<AIResource>();
            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(p =>
                {
                    p.RegisterLocalizedPage<KnowledgeBaseManagePage, AIResource>(
                        KnowledgeBaseManagePage.PAGE_URL,
                        "Pages:KnowledgeBaseManage:Title",
                        Icons.Material.Filled.Storage,
                        BuiltInNavigationCategoryIds.KnowledgeRetrieval,
                        addToNav: true,
                        navOrder: 3);
                });
        }
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        if (!Option.DisableKnowledgeBaseManagePage)
        {
            services.AddScoped<KnowledgeBaseManagePageState>();
        }
    }
}

/// <summary>
/// Configuration options for the knowledge-base UI module.
/// </summary>
public sealed class ModuleKnowledgeBaseUIOption : ModuleOptions<ModuleKnowledgeBaseUI>
{
    /// <summary>
    /// Disables the knowledge-base management page while keeping reusable selector components available.
    /// </summary>
    public bool DisableKnowledgeBaseManagePage { get; set; }
}

/// <summary>
/// Configuration guide for the knowledge-base UI module.
/// </summary>
public sealed class ModuleKnowledgeBaseUIGuide
    : ModuleGuide<ModuleKnowledgeBaseUI, ModuleKnowledgeBaseUIOption, ModuleKnowledgeBaseUIGuide>
{
}
