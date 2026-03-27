using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DataChannel.Pages;
using Monica.DataChannel.UIDataChannel.Services;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// UI module for DataChannel.
/// Provides the management interface for DataChannel.
/// </summary>
[ModuleKey(EMoModuleKey.DataChannelUI)]
public class ModuleDataChannelUI(ModuleDataChannelUIOption option)
    : MoModule<ModuleDataChannelUI, ModuleDataChannelUIOption, ModuleDataChannelUIGuide>(option)
{
    /// <summary>
    /// Configures services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register DataChannel services.
        services.AddScoped<DataChannelUIService>();
    }

    /// <summary>
    /// Declares module dependencies.
    /// </summary>
    public override void ClaimDependencies()
    {
        if (!Option.DisableDataChannelPage)
        {
            // Depend on the core DataChannel module.
            DependsOnModule<ModuleDataChannelGuide>().Register();

            // Depend on the UIStackTrace module for exception stack visualization.
            DependsOnModule<ModuleUIStackTraceGuide>().Register();

            // Depend on the UI core module and register the DataChannel page.
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UIDataChannelPage>(
                    UIDataChannelPage.PAGE_URL,
                    "Pages:DataChannelManage:Title",
                    Icons.Material.Filled.DataObject,
                    "Categories:Monitor",
                    addToNav: true,
                    navOrder: 30));
        }
    }
}

public static class ModuleDataChannelUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the DataChannelUI module.
        /// </summary>
        public static ModuleDataChannelUIGuide AddDataChannelUI(Action<ModuleDataChannelUIOption>? action = null)
        {
            return new ModuleDataChannelUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Guide for the DataChannel UI module.
/// </summary>
public class ModuleDataChannelUIGuide : MoModuleGuide<ModuleDataChannelUI, ModuleDataChannelUIOption, ModuleDataChannelUIGuide>
{
    /// <summary>
    /// Gets the requested configuration method keys.
    /// </summary>
    /// <returns>An array of configuration method keys.</returns>
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [];
    }
}

/// <summary>
/// Options for the DataChannel UI module.
/// </summary>
public class ModuleDataChannelUIOption : MoModuleOptionWithMinimalApi<ModuleDataChannelUI>
{
    /// <summary>
    /// Gets or sets a value indicating whether the DataChannel page is disabled.
    /// </summary>
    public bool DisableDataChannelPage { get; set; } = false;
}
