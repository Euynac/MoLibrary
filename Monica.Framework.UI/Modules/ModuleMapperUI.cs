using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Core.Modules;
using Monica.Framework.UI.Pages;

using Monica.UI.Modules;
using MudBlazor;

namespace Monica.Framework.UI.Modules;

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
    : MoModuleWithDependencies<ModuleMapperUI, ModuleMapperUIOption, ModuleMapperUIGuide>(option)
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
                .RegisterUIComponents(p => p.RegisterComponent<UIMapperPage>(
                    UIMapperPage.MAPPER_DEBUG_URL,
                    "Mapper调试",
                    Icons.Material.Filled.Code,
                    "调试",
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