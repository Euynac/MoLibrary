using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Localization.Models;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.UI.Localization;
using Monica.UI.Pages;
using Monica.UI.UISystemInfo.Models;
using Monica.UI.UISystemInfo.Support;
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
[ModuleKey(BuiltInModuleKey.SystemInfoUI)]
public class ModuleSystemInfoUI(ModuleSystemInfoUIOption option)
    : WebModuleBase<ModuleSystemInfoUI, ModuleSystemInfoUIOption, ModuleSystemInfoUIGuide>(option)
{
    /// <summary>
    /// Registers the services required by the system information UI.
    /// </summary>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<SystemInfoService>();
    }

    /// <summary>
    /// Declares localization, shell, and page dependencies for the system information UI.
    /// </summary>
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<SystemInfoResource>();

        if (!Option.DisablePage)
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

    /// <summary>
    /// Maps the system information API endpoints.
    /// </summary>
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
public class ModuleSystemInfoUIGuide : WebModuleGuide<ModuleSystemInfoUI, ModuleSystemInfoUIOption, ModuleSystemInfoUIGuide>
{
    private const string SwaggerLinkSecondKey = nameof(AddSwaggerLink);
    private const string SwaggerLinkNameKey = "CustomLinks:Items:Swagger:Name";
    private const string SwaggerLinkDescriptionKey = "CustomLinks:Items:Swagger:Description";
    private const string SwaggerLinkCategoryKey = "CustomLinks:Items:Swagger:Category";

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
        return AddCustomLink(new SystemInfoCustomLink
        {
            Name = LocalizedText.Plain(name),
            Url = url,
            Icon = icon ?? Icons.Material.Filled.Link,
            Description = LocalizedText.PlainOrNull(description),
            Category = LocalizedText.PlainOrNull(category),
            Order = order,
            Target = target,
            UserName = userName,
            Password = password
        }, secondKey: name);
    }

    /// <summary>
    /// Adds a localized quick link that opens the Swagger UI page from system information.
    /// This helper currently assumes Swagger UI is exposed at <c>/swagger</c>.
    /// </summary>
    /// <param name="order">Display order. Smaller values are shown first.</param>
    /// <param name="target">Target behavior used by the generated anchor element.</param>
    /// <returns>The current module guide.</returns>
    public ModuleSystemInfoUIGuide AddSwaggerLink(
        int order = 0,
        SystemInfoCustomLinkTarget target = SystemInfoCustomLinkTarget.NewTab)
    {
        return AddCustomLink(new SystemInfoCustomLink
        {
            Name = LocalizedText.Resource(SwaggerLinkNameKey, "API Management"),
            // TODO: Replace this hardcoded route after the Swagger module exposes its UI path as an option.
            Url = "/swagger",
            Icon = Icons.Material.Filled.Api,
            Description = LocalizedText.Resource(SwaggerLinkDescriptionKey, "View and test API endpoints."),
            Category = LocalizedText.Resource(SwaggerLinkCategoryKey, "Monica"),
            Order = order,
            Target = target
        }, secondKey: SwaggerLinkSecondKey);
    }

    private ModuleSystemInfoUIGuide AddCustomLink(SystemInfoCustomLink link, string secondKey)
    {
        ConfigureModuleOption(option =>
        {
            option.CustomLinks.Add(link);
        }, secondKey: secondKey);

        return this;
    }
}

/// <summary>
/// SystemInfoUI module options.
/// </summary>
public class ModuleSystemInfoUIOption : MinimalApiModuleOptions<ModuleSystemInfoUI>
{
    /// <summary>
    /// Gets or sets a value indicating whether the system information page is disabled.
    /// </summary>
    public bool DisablePage { get; set; }

    /// <summary>
    /// Gets the custom shortcut links displayed on the system information page.
    /// </summary>
    public List<SystemInfoCustomLink> CustomLinks { get; set; } = new();
}
