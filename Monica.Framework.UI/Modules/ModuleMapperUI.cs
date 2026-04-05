using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Framework.UI.Pages;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleMapperUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the MapperUI module
        /// </summary>
        public static ModuleMapperUIGuide AddMapperUI(Action<ModuleMapperUIOption>? action = null)
        {
            return new ModuleMapperUIGuide().Register(action);
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
            DependsOnModule<ModuleShellUIGuide>().Register()
                .ConfigureModuleOption(o => o.EnableMarkdown = true)
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UIMapperDebugPage>(
                    UIMapperDebugPage.PAGE_URL,
                    "Pages:MapperDebug:Title",
                    Icons.Material.Filled.Code,
                    "Categories:Debug",
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
