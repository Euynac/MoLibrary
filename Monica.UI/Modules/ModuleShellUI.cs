using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.UI.Localization;
using Monica.UI.Pages;
using Monica.UI.Shell.Components;
using Monica.UI.Shared.Components.Markdown;
using Monica.UI.Shell.State;
using Monica.UI.Shell.Support;
using MudBlazor;
using MudBlazor.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleShellUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the shell UI module.
        /// </summary>
        public static ModuleShellUIGuide AddUIShell(Action<ModuleShellUIOption>? action = null)
        {
            return new ModuleShellUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Shell UI module.
/// Provides the shared Blazor shell infrastructure for Monica UI modules.
/// </summary>
[ModuleKey(BuiltInModuleKey.UICore)]
public class ModuleShellUI(ModuleShellUIOption option)
    : WebModuleBase<ModuleShellUI, ModuleShellUIOption, ModuleShellUIGuide>(option)
{
    /// <summary>
    /// Declare module dependencies
    /// </summary>
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<UIRegistryResource>()
            .AddResource<SharedResource>();
    }

    public override void ConfigureBuilder(IHostApplicationBuilder builder)
    {
        if (builder is WebApplicationBuilder webBuilder && builder.Environment.IsStaging())
        {
            // Important pitfall:
            // UseStaticWebAssets is useful for debugging static resources (CSS/JS) from referenced RCLs in Visual Studio,
            // but it can cause 404 behavior in certain build/debug combinations and must not be relied on for production.
            // In production, static web assets should come from published output (`dotnet publish`).
            // https://github.com/MudBlazor/MudBlazor/issues/2793
            webBuilder.WebHost.UseStaticWebAssets();
            // In local/testing scenarios, generated static-web-asset mappings can be inspected via .StaticWebAssets.xml.
            // In production publish output, dependent static web assets are copied into the deployed wwwroot content.
            // https://learn.microsoft.com/en-us/aspnet/core/razor-pages/ui-class?view=aspnetcore-8.0&tabs=visual-studio#consume-content-from-a-referenced-rcl
            
            // If `/_framework/blazor.web.js` returns 404 in Debug, one possible cause is static-web-asset setup.
            // Another cause is an unexpected environment (for example, launchSettings.json not being applied).
            // Also verify `<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>` in the host .csproj when needed.
        }
    }

    /// <summary>
    /// Configuration service
    /// </summary>
    /// <param name="services">Service collection</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        // Add MudBlazor service
        services.AddMudServices();
        if(Option.EnableMarkdown)
        {
            services.AddMudMarkdownServices();   
        }

        // Add Razor component and interactive server component services
        services.AddRazorComponents()
            .AddInteractiveServerComponents(o =>
            {
                o.DetailedErrors = Option.EnableDebug;
            }).AddHubOptions(options =>
            {
                options.EnableDetailedErrors = Option.EnableDebug;
            });

        // Register UI component management service
        services.AddSingleton<IPageRegistry, PageRegistry>();

        // Register for browser storage service
        services.AddScoped<IBrowserStorage, BrowserStorage>();

        // Register theme service (Scoped: each Blazor circuit gets its own theme state)
        services.AddScoped<ThemeState>();
        services.AddScoped<IThemeState>(sp => sp.GetRequiredService<ThemeState>());

        // Register the default markdown asset resolver so Markdown components work without optional modules.
        services.TryAddScoped<IMoMarkdownAssetResolver, PassThroughMarkdownAssetResolver>();

        // Register user context service
        services.AddScoped<UserContextState>();
    }

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        var webApp = RequireWebApplication(app);

        webApp.MapStaticAssets();  // .NET 9 support

        // Antiforgery middleware must stay in the routed pipeline.
        app.UseAntiforgery();
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        var webApp = RequireWebApplication(app);
        var registry = app.ApplicationServices.GetRequiredService<IPageRegistry>();

        foreach (var redirect in Option.RouteRedirects)
        {
            var fromPath = redirect.Key;
            var toPath = redirect.Value;
            webApp.MapGet(fromPath, () => Results.LocalRedirect(toPath));
        }

        webApp.MapRazorComponents<AppShell>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(registry.GetAdditionalAssemblies());
        // Huge pitfall: if AddAdditionalAssemblies is missing here, pressing F5 refresh may return 404,
        // while navigation through Router may still work.
    }

    protected override int GetConfigureApplicationBuilderOrder()
    {
        return (int)ModuleApplicationMiddlewareOrder.AfterUseRouting;
    }

    protected override int GetConfigureEndpointsOrder()
    {
        return (int)ModuleRegistrationOrder.Normal;
    }

    private static WebApplication RequireWebApplication(IApplicationBuilder app)
    {
        return app as WebApplication
               ?? throw new InvalidOperationException(
                   $"{nameof(ModuleShellUI)} requires {nameof(WebApplication)} during application configuration.");
    }
}

/// <summary>
/// Shell UI module configuration guide.
/// </summary>
public class ModuleShellUIGuide : WebModuleGuide<ModuleShellUI, ModuleShellUIOption, ModuleShellUIGuide>
{

    /// <summary>
    /// Register UI components
    /// </summary>
    /// <param name="registrationAction">Component registration configuration action</param>
    /// <returns>Configuration Director</returns>
    public ModuleShellUIGuide RegisterUIComponents(Action<IPageRegistry> registrationAction)
    {
        // Perform component registration at application startup.
        ConfigureApplicationBuilder(builder =>
        {
            var registry = builder.ApplicationBuilder.ApplicationServices.GetRequiredService<IPageRegistry>();
            registrationAction(registry);
        }, ModuleApplicationMiddlewareOrder.BeforeUseRouting, secondKey: Guid.NewGuid().ToString());

        return this;
    }

    /// <summary>
    /// Add route redirection rules
    /// </summary>
    /// <param name="fromPath">Source path (such as "/")</param>
    /// <param name="toPath">Target path (such as "/swagger" or "~/swagger")</param>
    /// <returns>Configuration Director</returns>
    public ModuleShellUIGuide AddRouteRedirect(string fromPath, string toPath)
    {
        ConfigureModuleOption(option =>
        {
            option.RouteRedirects[fromPath] = toPath;
        }, secondKey: fromPath);

        return this;
    }

}

/// <summary>
/// Shell UI module options.
/// </summary>
public class ModuleShellUIOption : ModuleOptions<ModuleShellUI>
{
    /// <summary>
    /// App bar name
    /// </summary>
    public string UIAppBarName { get; set; } = nameof(Monica);

    /// <summary>
    /// Application version number
    /// </summary>
    public string UIAppVersion { get; set; } = "v1.0";

    /// <summary>
    /// Turn on Debug mode
    /// </summary>
    public bool EnableDebug { get; set; }

    /// <summary>
    /// Enable Markdown support
    /// </summary>
    public bool EnableMarkdown { get; set; }

    /// <summary>
    /// The maximum number of categories displayed in the top navigation bar (the excess will be placed in the "More" menu)
    /// </summary>
    public int MaxVisibleCategories { get; set; } = 6;

    /// <summary>
    /// Whether to enable the navigation bar search function
    /// </summary>
    public bool EnableNavBarSearch { get; set; } = true;

    /// <summary>
    /// A collection of routing redirection rules, where the key is the source path and the value is the target path.
    /// </summary>
    internal Dictionary<string, string> RouteRedirects { get; set; } = new();

    /// <summary>
    /// Whether to show the language switcher
    /// </summary>
    public bool ShowLanguageSwitcher { get; set; } = true;
}
