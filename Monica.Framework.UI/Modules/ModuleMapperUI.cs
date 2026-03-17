using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
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
        /// 配置 MapperUI 模块
        /// </summary>
        public static ModuleMapperUIGuide AddMapperUI(Action<ModuleMapperUIOption>? action = null)
        {
            return new ModuleMapperUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Mapper UI模块
/// </summary>
public class ModuleMapperUI(ModuleMapperUIOption option)
    : MoModule<ModuleMapperUI, ModuleMapperUIOption, ModuleMapperUIGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.MapperUI;
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableUIMapperPage)
        {
            DependsOnModule<ModuleMapperGuide>().Register();
            DependsOnModule<ModuleUICoreGuide>().Register()
                .ConfigureModuleOption(o=>o.EnableMarkdown = true)
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UIMapperPage>(
                    UIMapperPage.MAPPER_DEBUG_URL,
                    "Pages:MapperDebug:Title",
                    Icons.Material.Filled.Code,
                    "Categories:Debug",
                    addToNav: true,
                    navOrder: 10));
        }
    }
}

/// <summary>
/// MapperUI模块向导
/// </summary>
public class ModuleMapperUIGuide : MoModuleGuide<ModuleMapperUI, ModuleMapperUIOption, ModuleMapperUIGuide>
{
}

/// <summary>
/// MapperUI模块选项
/// </summary>
public class ModuleMapperUIOption : MoModuleOption<ModuleMapperUI>
{ 
    /// <summary>
    /// 是否禁用Mapper页面
    /// </summary>
    public bool DisableUIMapperPage { get; set; }
}