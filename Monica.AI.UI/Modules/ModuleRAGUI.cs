using Microsoft.Extensions.DependencyInjection;
using Monica.AI.Modules;
using Monica.AI.UI.Pages;
using Monica.AI.UI.Services;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.UI.Modules;
using MudBlazor;

namespace Monica.AI.UI.Modules;

/// <summary>
/// RAG UI module builder extensions.
/// </summary>
public static class ModuleRAGUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the RAG UI module.
        /// </summary>
        public static ModuleRAGUIGuide AddRAGUI(Action<ModuleRAGUIOption>? action = null)
        {
            return new ModuleRAGUIGuide().Register(action);
        }
    }
}

/// <summary>
/// RAG UI module.
/// Provides Blazor-based RAG debug and management interface.
/// </summary>
public class ModuleRAGUI(ModuleRAGUIOption option)
    : MoModuleWithDependencies<ModuleRAGUI, ModuleRAGUIOption, ModuleRAGUIGuide>(option)
{
    public override ModuleKey GetModuleKey() => EMoModuleKey.RAGUI;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<RAGUIService>();
    }

    public override void ClaimDependencies()
    {
        // Depends on RAG backend module
        DependsOnModule<ModuleRAGGuide>().Register();

        // Depends on UI core module and register RAG debug page
        if (!Option.DisableRAGDebugPage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p =>
                {
                    p.RegisterComponent<UIAIRAGDebugPage>(
                        UIAIRAGDebugPage.PAGE_URL,
                        "RAG Debug",
                        Icons.Material.Filled.ManageSearch,
                        "AI",
                        addToNav: true,
                        navOrder: 3);
                });
        }
    }
}

/// <summary>
/// RAG UI module configuration guide.
/// </summary>
public class ModuleRAGUIGuide
    : MoModuleGuide<ModuleRAGUI, ModuleRAGUIOption, ModuleRAGUIGuide>
{
}

/// <summary>
/// RAG UI module configuration options.
/// </summary>
public class ModuleRAGUIOption : MoModuleOption<ModuleRAGUI>
{
    /// <summary>
    /// Disable the RAG debug page.
    /// </summary>
    public bool DisableRAGDebugPage { get; set; }
}
