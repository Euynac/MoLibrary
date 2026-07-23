using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
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
        public ModuleMapperUIGuide AddMapperUI(Action<ModuleMapperUIOption>? action = null)
        {
            return builder.AddModule<ModuleMapperUI, ModuleMapperUIOption, ModuleMapperUIGuide>(action);
        }
    }
}

/// <summary>
/// Mapper UI module
/// </summary>
[ModuleKey(BuiltInModuleKey.MapperUI)]
public class ModuleMapperUI(ModuleMapperUIOption option)
    : ModuleBase<ModuleMapperUI, ModuleMapperUIOption, ModuleMapperUIGuide>(option)
{

    public override void ClaimDependencies()
    {
        if (!Option.DisablePage)
        {
            DependsOnModule<ModuleObjectMappingGuide>().Register();
            DependsOnModule<ModuleLocalizationGuide>().Register()
                .AddResource<MapperResource>();
            DependsOnModule<ModuleShellUIGuide>().Register()
                .ConfigureModuleOption(o => o.EnableMarkdown = true)
                .RegisterUIComponents(p => p.RegisterLocalizedPage<UIMapperDebugPage, MapperResource>(
                    UIMapperDebugPage.PAGE_URL,
                    "Pages:MapperDebug:Title",
                    Icons.Material.Filled.Code,
                    BuiltInNavigationCategoryIds.Debug,
                    addToNav: true,
                    navOrder: 10));
        }
    }
}

/// <summary>
/// MapperUI module wizard
/// </summary>
public class ModuleMapperUIGuide : ModuleGuide<ModuleMapperUI, ModuleMapperUIOption, ModuleMapperUIGuide>
{
}

/// <summary>
/// MapperUI module options
/// </summary>
public class ModuleMapperUIOption : ModuleOptions<ModuleMapperUI>
{ 
    /// <summary>
    /// Whether to disable Mapper pages
    /// </summary>
    public bool DisablePage { get; set; }
}
