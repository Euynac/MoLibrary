using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.DataChannel.Localization;
using Monica.DataChannel.Pages;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// UI module for DataChannel.
/// Provides the management interface for DataChannel.
/// </summary>
public class ModuleDataChannelUI : MonicaModule<ModuleDataChannelUIOption>, IUIModule
{
}

public static class ModuleDataChannelUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the DataChannelUI module.
        /// </summary>
        public ModuleRegistration<ModuleDataChannelUI, ModuleDataChannelUIOption> AddDataChannelUI(
            Action<ModuleDataChannelUIOption>? action = null)
        {
            var registration = builder.AddModule<ModuleDataChannelUI, ModuleDataChannelUIOption>(action);
            registration.Require<ModuleDataChannel, ModuleDataChannelOption>();
            registration.Require<ModuleStackTraceUI, ModuleStackTraceUIOption>();
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<DataChannelResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UIDataChannelPage, DataChannelResource>(
                    UIDataChannelPage.PAGE_URL,
                    "Pages:DataChannelManage:Title",
                    Icons.Material.Filled.DataObject,
                    BuiltInNavigationCategoryIds.Monitor,
                    addToNav: true,
                    navOrder: 30));
            return registration;
        }
    }
}

/// <summary>
/// Options for the DataChannel UI module.
/// </summary>
public class ModuleDataChannelUIOption : ModuleOptions<ModuleDataChannelUI>
{
}
