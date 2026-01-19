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
    /// <summary>
    /// 配置 AI UI 模块
    /// </summary>
    public static ModuleAIUIGuide ConfigModuleAIUI(this WebApplicationBuilder builder,
        Action<ModuleAIUIOption>? action = null)
    {
        return new ModuleAIUIGuide().Register(action);
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
/// AI UI 模块配置选项
/// </summary>
public class ModuleAIUIOption : MoModuleOption<ModuleAIUI>
{
    /// <summary>
    /// 禁用 AI 聊天页面
    /// </summary>
    public bool DisableAIChatPage { get; set; }

    /// <summary>
    /// 启用 Markdown 渲染
    /// </summary>
    public bool EnableMarkdown { get; set; } = true;

    /// <summary>
    /// 是否显示 Provider 选择器
    /// </summary>
    public bool ShowProviderSelector { get; set; } = true;

    /// <summary>
    /// 是否显示会话列表
    /// </summary>
    public bool ShowSessionList { get; set; } = true;

    /// <summary>
    /// 默认系统提示词（覆盖后端设置）
    /// </summary>
    public string? DefaultSystemPrompt { get; set; }

    /// <summary>
    /// 消息气泡最大宽度
    /// </summary>
    public string MessageMaxWidth { get; set; } = "80%";
}
