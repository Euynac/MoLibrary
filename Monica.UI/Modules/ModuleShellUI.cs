using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.UI.Localization;
using Monica.UI.Pages;
using Monica.UI.Shell.Components;
using Monica.UI.Shared.Components.Markdown;
using Monica.UI.Shell.State;
using Monica.UI.Shell.Support;
using Monica.UI.Theming;
using MudBlazor;
using MudBlazor.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleShellUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the shell UI module.
        /// </summary>
        public ModuleRegistration<ModuleShellUI, ModuleShellUIOption> AddUIShell(
            Action<ModuleShellUIOption>? action = null)
        {
            return builder.AddModule<ModuleShellUI, ModuleShellUIOption>(action);
        }
    }

    extension(ModuleRegistration<ModuleShellUI, ModuleShellUIOption> registration)
    {
        /// <summary>
        /// Registers a startup-only page or navigation contribution with the shell.
        /// </summary>
        /// <param name="registrationAction">The contribution applied before routing begins.</param>
        /// <returns>The same host-bound shell registration.</returns>
        public ModuleRegistration<ModuleShellUI, ModuleShellUIOption> RegisterUIComponents(
            Action<INavigationRegistryBuilder> registrationAction)
        {
            ArgumentNullException.ThrowIfNull(registrationAction);

            return registration.ConfigureApplicationBuilder(context =>
            {
                var registry = context.ApplicationBuilder.ApplicationServices
                    .GetRequiredService<INavigationRegistryBuilder>();
                registrationAction(registry);
            }, ModuleWebStage.BeforeRouting);
        }

        /// <summary>
        /// Adds a route redirect emitted by the shell during endpoint mapping.
        /// </summary>
        /// <param name="fromPath">The source route.</param>
        /// <param name="toPath">The redirect target.</param>
        /// <returns>The same host-bound shell registration.</returns>
        public ModuleRegistration<ModuleShellUI, ModuleShellUIOption> AddRouteRedirect(
            string fromPath,
            string toPath)
        {
            return registration.Configure(option =>
            {
                if (option.RouteRedirects.TryGetValue(fromPath, out var existingTarget))
                {
                    throw new InvalidOperationException(
                        $"Route redirect '{fromPath}' is already registered and points to '{existingTarget}'.");
                }

                option.RouteRedirects[fromPath] = toPath;
            });
        }
    }
}

/// <summary>
/// Shell UI module.
/// Provides the shared Blazor shell infrastructure for Monica UI modules.
/// </summary>
public class ModuleShellUI : MonicaModule<ModuleShellUIOption>, IWebHostRequiredModule, IUIModule
{
    /// <summary>
    /// Declare module dependencies
    /// </summary>
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleLocalization, ModuleLocalizationOption>(option =>
        {
            if (!option.ResourceMarkerTypes.Contains(typeof(SharedResource)))
            {
                option.ResourceMarkerTypes.Add(typeof(SharedResource));
            }
        });
    }

    /// <inheritdoc />
    public override void ConfigureBuilder(ModuleBuilderContext<ModuleShellUIOption> context)
    {
        var builder = context.HostApplicationBuilder;
        if (builder is not WebApplicationBuilder webBuilder)
        {
            return;
        }

        if (builder.Environment.IsProduction())
        {
            return;
        }

        // Monica UI modules are Razor class libraries and are commonly consumed only through NuGet packages.
        // Loading static web assets in the builder phase lets ASP.NET Core discover package-provided framework and
        // component assets before the shell maps static asset endpoints in local, staging, or other non-production hosts.
        webBuilder.WebHost.UseStaticWebAssets();
    }

    /// <summary>
    /// Configuration service
    /// </summary>
    /// <param name="context">The typed module composition context.</param>
    public override void ConfigureServices(ModuleContext<ModuleShellUIOption> context)
    {
        var services = context.Services;
        // Add MudBlazor service
        services.AddMudServices();
        if(Option.EnableMarkdown)
        {
            services.AddMudMarkdownServices();   
        }

        // Add Razor component and interactive server component services
        services.AddRazorComponents()
            .AddInteractiveServerComponents(circuitOptions =>
            {
                circuitOptions.DetailedErrors = Option.EnableDebug;
            }).AddHubOptions(options =>
            {
                options.EnableDetailedErrors = Option.EnableDebug;
            });

        // Keep startup contributions write-only and publish a separate immutable read catalog.
        services.AddSingleton<PageRegistry>();
        services.AddSingleton<INavigationRegistryBuilder>(
            static serviceProvider => serviceProvider.GetRequiredService<PageRegistry>());
        services.AddSingleton<IPageCatalog>(
            static serviceProvider => serviceProvider.GetRequiredService<PageRegistry>());

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

    public override void ConfigureApplicationBuilder(WebModuleContext<ModuleShellUIOption> context)
    {
        var app = context.ApplicationBuilder;
        var webApp = context.RequireWebApplication();

        webApp.MapStaticAssets()
            .WithMonicaEndpoint(MonicaEndpointKind.StaticAsset);  // .NET 9 support

        // Antiforgery middleware must stay in the routed pipeline.
        app.UseAntiforgery();
    }

    public override void ConfigureEndpoints(WebModuleContext<ModuleShellUIOption> context)
    {
        var app = context.ApplicationBuilder;
        var webApp = context.RequireWebApplication();
        var registry = app.ApplicationServices.GetRequiredService<PageRegistry>();
        registry.Seal();

        foreach (var redirect in Option.RouteRedirects)
        {
            var fromPath = redirect.Key;
            var toPath = redirect.Value;
            webApp.MapGet(fromPath, () => Results.LocalRedirect(toPath))
                .WithMonicaEndpoint(MonicaEndpointKind.Ui);
        }

        webApp.MapRazorComponents<AppShell>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(registry.GetAdditionalAssemblies())
            .WithMonicaEndpoint(MonicaEndpointKind.Ui);
        // Huge pitfall: if AddAdditionalAssemblies is missing here, pressing F5 refresh may return 404,
        // while navigation through Router may still work.
    }

    protected override ModuleWebStage GetApplicationBuilderStage() => ModuleWebStage.AfterRouting;

}

/// <summary>
/// Shell UI module options.
/// </summary>
public class ModuleShellUIOption : ModuleOptions<ModuleShellUI>
{
#if DEBUG
    private const bool EnableDebugDefault = true;
#else
    private const bool EnableDebugDefault = false;
#endif

    /// <summary>
    /// Application name displayed in the shell app bar.
    /// When not configured, the shell uses the application defaults configured through <see cref="IMonicaBuilder.ConfigureApplication"/>.
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// Application identifier displayed before the version badge.
    /// When not configured, the shell uses the application defaults configured through <see cref="IMonicaBuilder.ConfigureApplication"/>.
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// Application version displayed in the shell.
    /// When not configured, the shell uses the application defaults configured through <see cref="IMonicaBuilder.ConfigureApplication"/>
    /// and finally <c>v1.0</c>.
    /// </summary>
    public string? AppVersion { get; set; }

    /// <summary>
    /// Resolves the application name displayed in the shell app bar.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no explicit module value is configured and the option has not been bound to a Monica host.
    /// </exception>
    public string GetAppName() => MonicaApplicationOptions.Normalize(AppName)
                                  ?? Application.ResolveAppName(fallback: nameof(Monica));

    /// <summary>
    /// Resolves the application identifier displayed before the version badge.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no explicit module value is configured and the option has not been bound to a Monica host.
    /// </exception>
    public string GetAppId() => MonicaApplicationOptions.Normalize(AppId)
                                ?? Application.ResolveAppId();

    /// <summary>
    /// Resolves the application version displayed in the shell.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no explicit module value is configured and the option has not been bound to a Monica host.
    /// </exception>
    public string GetAppVersion() => MonicaApplicationOptions.Normalize(AppVersion)
                                     ?? Application.ResolveAppVersion(fallback: "v1.0")!;

    /// <summary>
    /// Enables UI debug diagnostics, including Blazor circuit detailed errors and SignalR hub detailed errors.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/> in DEBUG builds and <see langword="false"/> otherwise.
    /// Keep this disabled for production because detailed errors can expose sensitive information to clients.
    /// </remarks>
    public bool EnableDebug { get; set; } = EnableDebugDefault;

    /// <summary>
    /// Enable Markdown support
    /// </summary>
    public bool EnableMarkdown { get; set; }

    /// <summary>
    /// The theme applied when a browser initializes the Monica shell without a saved theme preference.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="MonicaThemeKind.Default"/>. Once the browser stores a user-selected theme, the stored
    /// preference takes precedence over this option on later visits.
    /// </remarks>
    public MonicaThemeKind DefaultTheme { get; set; } = MonicaThemeKind.Default;

    /// <summary>
    /// Whether the shell should start in dark mode when a browser has no saved theme preference.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/>. Once the browser stores a user-selected mode, the stored preference takes
    /// precedence over this option on later visits.
    /// </remarks>
    public bool DefaultDarkMode { get; set; }

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
