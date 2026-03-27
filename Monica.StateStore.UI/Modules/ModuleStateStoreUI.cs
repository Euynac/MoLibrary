using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.StateStore.UI.Pages;
using Monica.StateStore.UI.Localization;
using Monica.StateStore.UI.Services;
using Monica.StateStore.UI.Services.Browser;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleStateStoreUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the StateStoreUI module
        /// </summary>
        public static ModuleStateStoreUIGuide AddStateStoreUI(Action<ModuleStateStoreUIOption>? action = null)
        {
            return new ModuleStateStoreUIGuide().Register(action);
        }
    }
}

/// <summary>
/// StateStore UI module - provides state storage management interface
/// </summary>
[ModuleKey(EMoModuleKey.StateStoreUI)]
public class ModuleStateStoreUI(ModuleStateStoreUIOption option)
    : MoModule<ModuleStateStoreUI, ModuleStateStoreUIOption, ModuleStateStoreUIGuide>(option)
{

    public override void ClaimDependencies()
    {
        if (!Option.DisableStateStorePage)
        {
            DependsOnModule<ModuleLocalizationGuide>().Register()
                .AddResource<StateStoreResource>();

            // Depends on StateStore module
            DependsOnModule<ModuleStateStoreGuide>().Register();

            // Depend on the UI core module and register UI components
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(registry =>
                {
                    registry.RegisterLocalizedComponent<UIStateStoreDashboardPage>(
                        UIStateStoreDashboardPage.PAGE_URL,
                        "Pages:StateStoreManage:Title",
                        Icons.Material.Filled.Storage,
                        "Categories:Debug",
                        addToNav: true,
                        navOrder: 20);
                });
        }
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<StateStoreUIService>();
        services.AddSingleton<IStateStoreBrowserApi, RedisStateStoreBrowserApi>();
        services.AddSingleton<IStateStoreBrowserApi, MemoryStateStoreBrowserApi>();
        services.AddSingleton<IStateStoreBrowserApi, DaprStateStoreBrowserApi>();
        services.AddSingleton<IStateStoreBrowserApi, FallbackStateStoreBrowserApi>();
    }
}

/// <summary>
/// StateStore UI module configuration guide
/// </summary>
public class ModuleStateStoreUIGuide
    : MoModuleGuide<ModuleStateStoreUI, ModuleStateStoreUIOption, ModuleStateStoreUIGuide>
{
}

/// <summary>
/// StateStore UI module options
/// </summary>
public class ModuleStateStoreUIOption : MoModuleOption<ModuleStateStoreUI>
{
    /// <summary>
    /// Disable StateStore admin page
    /// </summary>
    public bool DisableStateStorePage { get; set; } = false;

    /// <summary>
    /// Default Key scan mode
    /// </summary>
    public string DefaultScanPattern { get; set; } = "*";

    /// <summary>
    /// Maximum number of keys per page
    /// </summary>
    public int MaxKeysPerPage { get; set; } = 50;

    /// <summary>
    /// Allow editing of Key (set to false for read-only mode)
    /// </summary>
    public bool AllowKeyEditing { get; set; } = true;

    /// <summary>
    /// Allow deletion of Key
    /// </summary>
    public bool AllowKeyDeletion { get; set; } = true;

    /// <summary>
    /// Allow creation of Key
    /// </summary>
    public bool AllowKeyCreation { get; set; } = true;
}
