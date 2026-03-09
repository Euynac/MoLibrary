using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Extensions;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Framework.UI.UISystemInfo.Models;
using Monica.Framework.UI.UISystemInfo.Services;
using Monica.Framework.UI.Pages;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSystemInfoUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 SystemInfoUI 模块
        /// </summary>
        public static ModuleSystemInfoUIGuide AddSystemInfoUI(Action<ModuleSystemInfoUIOption>? action = null)
        {
            return new ModuleSystemInfoUIGuide().Register(action);
        }
    }
}

/// <summary>
/// 系统信息UI模块
/// </summary>
public class ModuleSystemInfoUI(ModuleSystemInfoUIOption option)
    : MoModuleWithDependencies<ModuleSystemInfoUI, ModuleSystemInfoUIOption, ModuleSystemInfoUIGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.SystemInfoUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<SystemInfoService>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableUISystemInfoPage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UISystemInfoPage>(
                    UISystemInfoPage.PAGE_URL,
                    "Pages:SystemInfo:Title",
                    Icons.Material.Filled.Info,
                    "Categories:Monitor",
                    addToNav: true,
                    navOrder: 50));
        }
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            endpoints.MapGet("/system/info",
                async ([FromQuery] bool? simple,
                      [FromServices] SystemInfoService systemInfoService) =>
                {
                    var result = await systemInfoService.GetSystemInfoAsync(simple);
                    return result.GetResponse();
                })
                .WithName("获取微服务信息")
                .WithTags(tagName)
                .WithSummary("获取微服务信息")
                .WithDescription("获取微服务信息");
        });
    }
}

/// <summary>
/// SystemInfoUI模块向导
/// </summary>
public class ModuleSystemInfoUIGuide : MoModuleGuide<ModuleSystemInfoUI, ModuleSystemInfoUIOption, ModuleSystemInfoUIGuide>
{
    /// <summary>
    /// 添加自定义快捷链接到系统信息页面
    /// </summary>
    /// <param name="name">链接显示名称</param>
    /// <param name="url">链接URL（支持相对路径如 "/swagger" 或绝对URL如 "https://example.com"）</param>
    /// <param name="icon">MudBlazor Material Icon 字符串（默认为链接图标）</param>
    /// <param name="description">链接描述/提示文本</param>
    /// <param name="category">分类/分组名称（相同分类的链接会分组显示）</param>
    /// <param name="order">显示顺序（数字越小越靠前，默认为0）</param>
    /// <param name="target">链接打开方式（默认 _blank 新标签页）</param>
    /// <returns></returns>
    public ModuleSystemInfoUIGuide AddCustomLink(
        string name,
        string url,
        string? icon = null,
        string? description = null,
        string? category = null,
        int order = 0,
        string target = "_blank")
    {
        ConfigureModuleOption(option =>
        {
            option.CustomLinks.Add(new SystemInfoCustomLink
            {
                Name = name,
                Url = url,
                Icon = icon ?? Icons.Material.Filled.Link,
                Description = description,
                Category = category,
                Order = order,
                Target = target
            });
        }, secondKey: name);

        return this;
    }
}

/// <summary>
/// SystemInfoUI模块选项
/// </summary>
public class ModuleSystemInfoUIOption : MoModuleOptionWithMinimalApi<ModuleSystemInfoUI>
{
    /// <summary>
    /// 是否禁用系统信息页面
    /// </summary>
    public bool DisableUISystemInfoPage { get; set; }

    /// <summary>
    /// 自定义快捷链接列表，显示在系统信息页面卡片中
    /// </summary>
    public List<SystemInfoCustomLink> CustomLinks { get; set; } = new();
} 