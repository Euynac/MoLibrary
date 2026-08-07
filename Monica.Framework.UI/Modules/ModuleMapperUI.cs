using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Framework.UI.Pages;
using Monica.Framework.UI.Localization;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleMapperUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the MapperUI module
        /// </summary>
        public ModuleRegistration<ModuleMapperUI, ModuleMapperUIOption> AddMapperUI(
            Action<ModuleMapperUIOption>? action = null)
        {
            var registration = builder.AddModule<ModuleMapperUI, ModuleMapperUIOption>(action);
            registration.Require<ModuleObjectMapping, ModuleObjectMappingOption>();
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<MapperResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>(option => option.EnableMarkdown = true)
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UIMapperDebugPage, MapperResource>(
                    UIMapperDebugPage.PAGE_URL,
                    "Pages:MapperDebug:Title",
                    Icons.Material.Filled.Code,
                    BuiltInNavigationCategoryIds.Debug,
                    addToNav: true,
                    navOrder: 10));
            return registration;
        }
    }
}

/// <summary>
/// Mapper UI module
/// </summary>
public class ModuleMapperUI : MonicaModule<ModuleMapperUIOption>, IUIModule
{
}

/// <summary>
/// MapperUI module options
/// </summary>
public class ModuleMapperUIOption : ModuleOptions<ModuleMapperUI>
{
}
