using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.SignalR.Localization;
using Monica.SignalR.Pages;
using Monica.SignalR.UISignalR.State;
using Monica.SignalR.UISignalR.Support;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions used to register the SignalR debug UI module.
/// </summary>
public static class ModuleSignalRUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the SignalR debug UI module and applies optional module configuration.
        /// </summary>
        /// <param name="action">Optional module option configuration delegate.</param>
        /// <returns>The host-bound SignalR UI module registration.</returns>
        public ModuleRegistration<ModuleSignalRUI, ModuleSignalRUIOption> AddSignalRUI(Action<ModuleSignalRUIOption>? action = null)
        {
            var module = builder.AddModule<ModuleSignalRUI, ModuleSignalRUIOption>(action);
            module.Require<ModuleLocalization, ModuleLocalizationOption>().AddResource<SignalRResource>();
            module.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UISignalRDebugPage, SignalRResource>(
                    UISignalRDebugPage.PAGE_URL,
                    "Pages:SignalRDebug:Title",
                    Icons.Material.Filled.Settings,
                    BuiltInNavigationCategoryIds.Debug,
                    addToNav: true,
                    navOrder: 20));
            return module;
        }
    }
}

/// <summary>
/// UI module that contributes the SignalR debug page and its supporting client-side state services.
/// </summary>
public class ModuleSignalRUI : MonicaModule<ModuleSignalRUIOption>, IUIModule
{
    public override void ConfigureServices(ModuleContext<ModuleSignalRUIOption> context)
    {
        var services = context.Services;
        services.AddScoped<SignalRDebugPageStateFactory>();
        services.AddScoped<SignalRInvocationArgumentParser>();
    }

    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleLocalization, ModuleLocalizationOption>();
        module.Require<ModuleSignalR, ModuleSignalROption>();
        module.Require<ModuleShellUI, ModuleShellUIOption>();
    }
}

/// <summary>
/// Registration extensions for the SignalR debug UI module.
/// </summary>


/// <summary>
/// Configuration options for the SignalR debug UI module.
/// </summary>
public class ModuleSignalRUIOption : ModuleOptions<ModuleSignalRUI>
{
    /// <summary>
    /// Gets or sets the default bearer token prefilled on the SignalR debug page before a browser connection is opened.
    /// </summary>
    public string? DefaultAccessToken { get; set; }
}
