using Microsoft.Extensions.DependencyInjection;
using Monica.AI.UI.Localization;
using Monica.AI.UI.Pages;
using Monica.AI.UI.UIChat.State;
using Monica.AI.UI.UIChat.Support;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// AI UI module registration extension method
/// </summary>
public static class ModuleAIUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure AI UI module
        /// </summary>
        public static ModuleAIUIGuide AddAIUI(Action<ModuleAIUIOption>? action = null)
        {
            return new ModuleAIUIGuide().Register(action);
        }
    }
}

/// <summary>
/// AI UI module implementation
/// Provides an AI chat interface based on Blazor
/// </summary>
[ModuleKey(BuiltInModuleKey.AIUI)]
public class ModuleAIUI(ModuleAIUIOption option)
    : ModuleBase<ModuleAIUI, ModuleAIUIOption, ModuleAIUIGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register UI services
        services.AddScoped<ChatPageState>();
        services.AddScoped<ChatSessionStore>();
    }

    public override void ClaimDependencies()
    {
        // Depends on backend AI modules
        DependsOnModule<ModuleAIGuide>().Register();

        if (!Option.DisableAIChatPage)
        {
            DependsOnModule<ModuleKnowledgeBaseUIGuide>().Register();
        }

        if (!Option.DisableAIChatPage || !Option.DisableAIProviderPage)
        {
            DependsOnModule<ModuleLocalizationGuide>().Register()
                .AddResource<AIResource>();
        }

        // Depend on the UI core module and register the page
        if (!Option.DisableAIChatPage)
        {
            DependsOnModule<ModuleShellUIGuide>().Register(o => o.EnableMarkdown = true)
                .RegisterUIComponents(p =>
                {
                    p.RegisterLocalizedComponent<ChatPage>(
                        ChatPage.PAGE_URL,
                        "Pages:AIChat:Title",
                        Icons.Material.Filled.SmartToy,
                        "Categories:AI",
                        addToNav: true,
                        navOrder: 1);
                });
        }

        if (!Option.DisableAIProviderPage)
        {
            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(p =>
                {
                    p.RegisterLocalizedComponent<ProviderManagePage>(
                        ProviderManagePage.PAGE_URL,
                        "Pages:AIProviderManage:Title",
                        Icons.Material.Filled.Hub,
                        "Categories:AI",
                        addToNav: true,
                        navOrder: 2);
                });
        }
    }
}

/// <summary>
/// AI UI module configuration guide
/// </summary>
public class ModuleAIUIGuide
    : ModuleGuide<ModuleAIUI, ModuleAIUIOption, ModuleAIUIGuide>
{
}

/// <summary>
/// AI UI module configuration options
/// </summary>
public class ModuleAIUIOption : ModuleOptions<ModuleAIUI>
{
    /// <summary>
    /// Disable the AI chat page
    /// </summary>
    public bool DisableAIChatPage { get; set; }

    /// <summary>
    /// Disable the AI provider manage page
    /// </summary>
    public bool DisableAIProviderPage { get; set; }

    /// <summary>
    /// Enable Markdown rendering
    /// </summary>
    public bool EnableMarkdown { get; set; } = true;

    /// <summary>
    /// Show provider selector
    /// </summary>
    public bool ShowProviderSelector { get; set; } = true;

    /// <summary>
    /// Show session list
    /// </summary>
    public bool ShowSessionList { get; set; } = true;

    /// <summary>
    /// Default system prompt (overrides backend setting)
    /// </summary>
    public string? DefaultSystemPrompt { get; set; }

    /// <summary>
    /// Maximum width for message bubbles
    /// </summary>
    public string MessageMaxWidth { get; set; } = "80%";

    /// <summary>
    /// Request timeout in milliseconds (0 = no timeout)
    /// </summary>
    public int RequestTimeoutMs { get; set; } = 60000;

    /// <summary>
    /// Show connection status indicator
    /// </summary>
    public bool ShowConnectionStatus { get; set; }

    /// <summary>
    /// Enable auto-scroll to bottom on new messages
    /// </summary>
    public bool EnableAutoScroll { get; set; } = true;

    /// <summary>
    /// Default knowledge base IDs to pre-select in the chat UI.
    /// </summary>
    public List<string> DefaultKnowledgeBaseIds { get; set; } = [];

    /// <summary>
    /// Whether to show the knowledge base selector in the chat UI.
    /// </summary>
    public bool ShowKnowledgeBaseSelector { get; set; } = true;
}
