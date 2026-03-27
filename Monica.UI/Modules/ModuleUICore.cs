using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Dashboard;
using Monica.Core.Modularity.Dashboard.Interfaces;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.UI.Localization;
using Monica.UI.Components;
using Monica.UI.Components.Pages;
using Monica.UI.Components.Markdown;
using MudBlazor;
using MudBlazor.Services;
using Monica.UI.UICore.Interfaces;
using Monica.UI.UICore.Services;
using Monica.UI.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleUICoreBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the UICore module
        /// </summary>
        public static ModuleUICoreGuide AddUICore(Action<ModuleUICoreOption>? action = null)
        {
            return new ModuleUICoreGuide().Register(action).AddBasicMiddlewares();
        }
    }
}

/// <summary>
/// UI core module
/// Provides MudBlazor-based UI infrastructure
/// </summary>
[ModuleKey(EMoModuleKey.UICore)]
public class ModuleUICore(ModuleUICoreOption option)
    : MoModule<ModuleUICore, ModuleUICoreOption, ModuleUICoreGuide>(option)
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

        // Register module system status service
        if (!Option.DisableModuleSystemUI)
        {
            services.AddSingleton<IModuleSystemStatusService, ModuleSystemStatusService>();
        }

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
        services.AddSingleton<IUIComponentRegistry, UIComponentRegistry>();

        // Register for browser storage service
        services.AddScoped<IMoBrowserStorage, MoBrowserStorage>();

        // Register theme service (Scoped: each Blazor circuit gets its own theme state)
        services.AddScoped<MoThemeService>();
        services.AddScoped<IMoThemeService>(sp => sp.GetRequiredService<MoThemeService>());

        // Register the default markdown asset resolver so Markdown components work without optional modules.
        services.TryAddScoped<IMoMarkdownAssetResolver, PassThroughMarkdownAssetResolver>();

        // Register user context service
        services.AddScoped<MoUserContextService>();
    }
}

/// <summary>
/// UI core module configuration guide
/// </summary>
public class ModuleUICoreGuide : MoModuleGuide<ModuleUICore, ModuleUICoreOption, ModuleUICoreGuide>
{

    /// <summary>
    /// Register UI components
    /// </summary>
    /// <param name="registrationAction">Component registration configuration action</param>
    /// <returns>Configuration Director</returns>
    public ModuleUICoreGuide RegisterUIComponents(Action<IUIComponentRegistry> registrationAction)
    {
        // Perform component registration at application startup.
        ConfigureApplicationBuilder(builder =>
        {
            var registry = builder.ApplicationBuilder.ApplicationServices.GetRequiredService<IUIComponentRegistry>();
            registrationAction(registry);
        }, EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting, secondKey: Guid.NewGuid().ToString());

        return this;
    }

    /// <summary>
    /// Add route redirection rules
    /// </summary>
    /// <param name="fromPath">Source path (such as "/")</param>
    /// <param name="toPath">Target path (such as "/swagger" or "~/swagger")</param>
    /// <returns>Configuration Director</returns>
    public ModuleUICoreGuide AddRouteRedirect(string fromPath, string toPath)
    {
        ConfigureModuleOption(option =>
        {
            option.RouteRedirects[fromPath] = toPath;
        }, secondKey: fromPath);

        return this;
    }

    /// <summary>
    /// Add UI basic middleware
    /// NOTE: These middlewares should be called by the host application
    /// </summary>
    /// <returns>Configuration Director</returns>
    public ModuleUICoreGuide AddBasicMiddlewares()
    {
        ConfigureApplicationBuilder(builder =>
        {
            var app = builder.RequireWebApplication();

            //app.UseExceptionHandler("/Error", createScopeForErrors: true);

            app.MapStaticAssets();  // .NET 9 support

            // //Static file support (for MudBlazor resources and Razor class library static resources)
            // app.UseStaticFiles();
            
            //app.UseStaticFiles(new StaticFileOptions()
            //{
            //    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "CustomStyles")),
            //    RequestPath = new PathString("/CustomStyles")
            //});

            // Antiforgery middleware:
            // Call app.UseAntiforgery() after authentication/authorization and within the routing pipeline.
            builder.ApplicationBuilder.UseAntiforgery();

        }, EMoModuleApplicationMiddlewaresOrder.AfterUseRouting);

        ConfigureEndpoints(builder =>
        {
            var app = builder.RequireWebApplication();
            var registry = builder.ApplicationBuilder.ApplicationServices.GetRequiredService<IUIComponentRegistry>();

            if (!builder.ModuleOption.DisableModuleSystemUI)
            {
                registry.RegisterLocalizedComponent<ModuleSystemDashboard>(ModuleSystemDashboard.MODULE_SYSTEM_DASHBOARD_URL, "Pages:ModuleSystemDashboard:Title", Icons.Material.Filled.Dashboard, "Categories:Module", true, navOrder: 10);
            }

            // Configure route redirection
            foreach (var redirect in builder.ModuleOption.RouteRedirects)
            {
                var fromPath = redirect.Key;
                var toPath = redirect.Value;
                app.MapGet(fromPath, () => Results.LocalRedirect(toPath));
            }

            app.MapRazorComponents<MoApp>()
                .AddInteractiveServerRenderMode().AddAdditionalAssemblies(registry.GetAdditionalAssemblies());
            // Huge pitfall: if AddAdditionalAssemblies is missing here, pressing F5 refresh may return 404,
            // while navigation through Router may still work.
        });

        return this;
    }
}

/// <summary>
/// UI core module options
/// </summary>
public class ModuleUICoreOption : MoModuleOption<ModuleUICore>
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
    /// Disable module system UI interface
    /// </summary>
    public bool DisableModuleSystemUI { get; set; }

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
