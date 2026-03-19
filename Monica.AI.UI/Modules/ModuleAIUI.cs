using Microsoft.Extensions.DependencyInjection;
using Monica.AI.UI.Localization;
using Monica.AI.UI.Pages;
using Monica.AI.UI.Services;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// AI UI 模块注册扩展方法
/// </summary>
public static class ModuleAIUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 AI UI 模块
        /// </summary>
        public static ModuleAIUIGuide AddAIUI(Action<ModuleAIUIOption>? action = null)
        {
            return new ModuleAIUIGuide().Register(action);
        }
    }
}

/// <summary>
/// AI UI 模块实现
/// 提供基于 Blazor 的 AI 聊天界面
/// </summary>
[ModuleKey(EMoModuleKey.AIUI)]
public class ModuleAIUI(ModuleAIUIOption option)
    : MoModule<ModuleAIUI, ModuleAIUIOption, ModuleAIUIGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register UI services
        services.AddScoped<AIChatUIService>();
        services.AddScoped<ChatSessionStorage>();
        services.AddScoped<ChatSessionStateManager>();
        services.AddScoped<AIProviderUIService>();
    }

    public override void ClaimDependencies()
    {
        // 依赖后端 AI 模块
        DependsOnModule<ModuleAIGuide>().Register();

        if (!Option.DisableAIChatPage || !Option.DisableAIProviderPage)
        {
            DependsOnModule<ModuleLocalizationGuide>().Register()
                .AddResource<AIResource>();
        }

        // 依赖 UI 核心模块并注册页面
        if (!Option.DisableAIChatPage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register(o => o.EnableMarkdown = true)
                .RegisterUIComponents(p =>
                {
                    p.RegisterLocalizedComponent<UIAIChatPage>(
                        UIAIChatPage.PAGE_URL,
                        "Pages:AIChat:Title",
                        Icons.Material.Filled.SmartToy,
                        "Categories:AI",
                        addToNav: true,
                        navOrder: 1);
                });
        }

        if (!Option.DisableAIProviderPage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p =>
                {
                    p.RegisterLocalizedComponent<UIAIProviderManagePage>(
                        UIAIProviderManagePage.PAGE_URL,
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
/// AI UI 模块配置指南
/// </summary>
public class ModuleAIUIGuide
    : MoModuleGuide<ModuleAIUI, ModuleAIUIOption, ModuleAIUIGuide>
{
}

/// <summary>
/// AI UI module configuration options
/// </summary>
public class ModuleAIUIOption : MoModuleOption<ModuleAIUI>
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
