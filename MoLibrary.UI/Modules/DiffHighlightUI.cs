using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.UI.Components.Pages;
using MudBlazor;

namespace MoLibrary.UI.Modules;

/// <summary>
/// DiffHighlightUI模块构建器扩展
/// </summary>
public static class ModuleDiffHighlightUIBuilderExtensions
{
    public static ModuleDiffHighlightUIGuide ConfigModuleDiffHighlightUI(this WebApplicationBuilder builder,
        Action<ModuleDiffHighlightUIOption>? action = null)
    {
        return new ModuleDiffHighlightUIGuide().Register(action);
    }
}

/// <summary>
/// 文本差异对比高亮UI模块
/// </summary>
public class ModuleDiffHighlightUI(ModuleDiffHighlightUIOption option)
    : MoModuleWithDependencies<ModuleDiffHighlightUI, ModuleDiffHighlightUIOption, ModuleDiffHighlightUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DiffHighlightUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // 无需添加额外的服务，直接使用源模块的DiffHighlightService
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableDiffHighlightPage)
        {
            DependsOnModule<ModuleDiffHighlightGuide>().Register();
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<UIDiffHighlightPage>(
                    UIDiffHighlightPage.DIFF_HIGHLIGHT_URL, 
                    "文本差异对比", 
                    Icons.Material.Filled.Compare, 
                    "开发工具", 
                    addToNav: true, 
                    navOrder: 110));
        }
    }
}

/// <summary>
/// DiffHighlightUI模块向导
/// </summary>
public class ModuleDiffHighlightUIGuide : MoModuleGuide<ModuleDiffHighlightUI, ModuleDiffHighlightUIOption, ModuleDiffHighlightUIGuide>
{
}

/// <summary>
/// DiffHighlightUI模块选项
/// </summary>
public class ModuleDiffHighlightUIOption : MoModuleOption<ModuleDiffHighlightUI>
{ 
    /// <summary>
    /// 是否禁用差异对比页面
    /// </summary>
    public bool DisableDiffHighlightPage { get; set; }
}