using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.AI.Modules;
using MoLibrary.AI.UI.Pages;
using MoLibrary.AI.UI.Services;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.AI.UI.Modules;

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
        public static ModuleAIUIGuide ConfigModuleAIUI(Action<ModuleAIUIOption>? action = null)
        {
            return new ModuleAIUIGuide().Register(action);
        }
    }
}

/// <summary>
/// AI UI 模块实现
/// 提供基于 Blazor 的 AI 聊天界面
/// </summary>
public class ModuleAIUI(ModuleAIUIOption option)
    : MoModuleWithDependencies<ModuleAIUI, ModuleAIUIOption, ModuleAIUIGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.AIUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册 UI 聊天服务
        services.AddScoped<AIChatUIService>();
        services.AddScoped<ChatSessionStorage>();
    }

    public override void ClaimDependencies()
    {
        // 依赖后端 AI 模块
        DependsOnModule<ModuleAIGuide>().Register();

        // 依赖 UI 核心模块并注册页面
        if (!Option.DisableAIChatPage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register(o => o.EnableMarkdown = true)
                .RegisterUIComponents(p =>
                {
                    p.RegisterComponent<UIAIChatPage>(
                        UIAIChatPage.PAGE_URL,
                        "AI 助手",
                        Icons.Material.Filled.SmartToy,
                        "AI",
                        addToNav: true,
                        navOrder: 1);
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
}
