using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.FrameworkUI.Pages;

using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.FrameworkUI.Modules;

/// <summary>
/// MapperUI模块构建器扩展
/// </summary>
public static class ModuleMapperUIBuilderExtensions
{
    public static ModuleMapperUIGuide ConfigModuleMapperUI(this WebApplicationBuilder builder,
        Action<ModuleMapperUIOption>? action = null)
    {
        return new ModuleMapperUIGuide().Register(action);
    }
}

/// <summary>
/// Mapper UI模块
/// </summary>
public class ModuleMapperUI(ModuleMapperUIOption option)
    : MoModuleWithDependencies<ModuleMapperUI, ModuleMapperUIOption, ModuleMapperUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.MapperUI;
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
                    "系统管理", 
                    addToNav: true, 
                    navOrder: 90));
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