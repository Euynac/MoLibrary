using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.SignalR.Localization;
using Monica.SignalR.Pages;
using Monica.SignalR.UISignalR.State;
using Monica.SignalR.UISignalR.Support;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions used to register the SignalR debug UI module.
/// </summary>
public static class ModuleSignalRUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers the SignalR debug UI module and applies optional module configuration.
        /// </summary>
        /// <param name="action">Optional module option configuration delegate.</param>
        /// <returns>Returns the module guide used to continue SignalR UI registration.</returns>
        public static ModuleSignalRUIGuide AddSignalRUI(Action<ModuleSignalRUIOption>? action = null)
        {
            return new ModuleSignalRUIGuide().Register(action);
        }
    }
}

/// <summary>
/// UI module that contributes the SignalR debug page and its supporting client-side state services.
/// </summary>
/// <param name="option">The module configuration options.</param>
[ModuleKey(EMoModuleKey.SignalRUI)]
public class ModuleSignalRUI(ModuleSignalRUIOption option)
    : MoModule<ModuleSignalRUI, ModuleSignalRUIOption, ModuleSignalRUIGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<SignalRDebugPageState>();
        services.AddScoped<SignalRDebugJsClient>();
        services.AddScoped<SignalRInvocationArgumentParser>();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<SignalRResource>();

        if (!Option.DisableDebugPage)
        {
            DependsOnModule<ModuleSignalRGuide>().Register();
            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(registry => registry.RegisterLocalizedComponent<UISignalRDebugPage>(
                    UISignalRDebugPage.PAGE_URL,
                    "Pages:SignalRDebug:Title",
                    Icons.Material.Filled.Settings,
                    "Categories:Debug",
                    addToNav: true,
                    navOrder: 20));
        }
    }
}

/// <summary>
/// Fluent registration guide for the SignalR debug UI module.
/// </summary>
public class ModuleSignalRUIGuide : MoModuleGuide<ModuleSignalRUI, ModuleSignalRUIOption, ModuleSignalRUIGuide>
{
}

/// <summary>
/// Configuration options for the SignalR debug UI module.
/// </summary>
public class ModuleSignalRUIOption : MoModuleOption<ModuleSignalRUI>
{
    /// <summary>
    /// Gets or sets a value indicating whether the SignalR debug page should be excluded from UI registration.
    /// </summary>
    public bool DisableDebugPage { get; set; }

    /// <summary>
    /// Gets or sets the default bearer token prefilled on the SignalR debug page before a browser connection is opened.
    /// </summary>
    public string? DefaultAccessToken { get; set; }
}
