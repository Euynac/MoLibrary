using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.Framework.UI.UISystemInfo.Models;
using MoLibrary.Framework.UI.UISystemInfo.Services;
using MoLibrary.Framework.UI.Pages;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.Framework.UI.Modules;

/// <summary>
/// SystemInfoUI模块构建器扩展
/// </summary>
public static class ModuleSystemInfoUIBuilderExtensions
{
    public static ModuleSystemInfoUIGuide ConfigModuleSystemInfoUI(this WebApplicationBuilder builder,
        Action<ModuleSystemInfoUIOption>? action = null)
    {
        return new ModuleSystemInfoUIGuide().Register(action);
    }
}

/// <summary>
/// 系统信息UI模块
/// </summary>
public class ModuleSystemInfoUI(ModuleSystemInfoUIOption option)
    : MoModuleWithDependencies<ModuleSystemInfoUI, ModuleSystemInfoUIOption, ModuleSystemInfoUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.SystemInfoUI;
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
                .RegisterUIComponents(p => p.RegisterComponent<UISystemInfoPage>(
                    UISystemInfoPage.PAGE_URL,
                    "系统信息",
                    Icons.Material.Filled.Info,
                    "监控",
                    addToNav: true,
                    navOrder: 50));
        }
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = Option.GetApiGroupName(), Description = "系统信息相关接口" } };

            endpoints.MapGet("/system/info",
                async ([FromQuery] bool? simple,
                      [FromServices] SystemInfoService systemInfoService) =>
                {
                    var result = await systemInfoService.GetSystemInfoAsync(simple);
                    return result.GetResponse();
                })
                .WithName("获取微服务信息").WithOpenApi(operation =>
                {
                    operation.Summary = "获取微服务信息";
                    operation.Description = "获取微服务信息";
                    operation.Tags = tagGroup;
                    return operation;
                });
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