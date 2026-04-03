using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Framework.UI.Localization;
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
        /// Configures the SystemInfoUI module.
        /// </summary>
        public static ModuleSystemInfoUIGuide AddSystemInfoUI(Action<ModuleSystemInfoUIOption>? action = null)
        {
            return new ModuleSystemInfoUIGuide().Register(action);
        }
    }
}

/// <summary>
/// System information UI module.
/// </summary>
[ModuleKey(EMoModuleKey.SystemInfoUI)]
public class ModuleSystemInfoUI(ModuleSystemInfoUIOption option)
    : MoModule<ModuleSystemInfoUI, ModuleSystemInfoUIOption, ModuleSystemInfoUIGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<SystemInfoService>();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<SystemInfoResource>();

        if (!Option.DisableUISystemInfoPage)
        {
            DependsOnModule<ModuleShellUIGuide>().Register()
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
/// SystemInfoUI module guide.
/// </summary>
public class ModuleSystemInfoUIGuide : MoModuleGuide<ModuleSystemInfoUI, ModuleSystemInfoUIOption, ModuleSystemInfoUIGuide>
{
    /// <summary>
    /// Adds a custom shortcut link to the system information page.
    /// </summary>
    /// <param name="name">Display name of the link.</param>
    /// <param name="url">Link URL. Supports relative paths such as "/swagger" and absolute URLs.</param>
    /// <param name="icon">MudBlazor Material icon string. Defaults to the generic link icon.</param>
    /// <param name="description">Optional description shown under the link.</param>
    /// <param name="category">Optional grouping label used to organize shortcuts.</param>
    /// <param name="order">Display order. Smaller values are shown first.</param>
    /// <param name="target">Target behavior used by the generated anchor element.</param>
    /// <param name="userName">Optional user name shown for quick copy.</param>
    /// <param name="password">Optional password exposed only through a copy action.</param>
    public ModuleSystemInfoUIGuide AddCustomLink(
        string name,
        string url,
        string? icon = null,
        string? description = null,
        string? category = null,
        int order = 0,
        SystemInfoCustomLinkTarget target = SystemInfoCustomLinkTarget.NewTab,
        string? userName = null,
        string? password = null)
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
                Target = target,
                UserName = userName,
                Password = password
            });
        }, secondKey: name);

        return this;
    }
}

/// <summary>
/// SystemInfoUI module options.
/// </summary>
public class ModuleSystemInfoUIOption : MoModuleOptionWithMinimalApi<ModuleSystemInfoUI>
{
    /// <summary>
    /// Gets or sets a value indicating whether the system information page is disabled.
    /// </summary>
    public bool DisableUISystemInfoPage { get; set; }

    /// <summary>
    /// Gets the custom shortcut links displayed on the system information page.
    /// </summary>
    public List<SystemInfoCustomLink> CustomLinks { get; set; } = new();
} 
