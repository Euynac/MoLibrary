using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Core.Modules;
using Monica.UI.Components.Pages;
using MudBlazor;

namespace Monica.UI.Modules;

public static class ModuleDiffHighlightUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 DiffHighlightUI 模块
        /// </summary>
        public static ModuleDiffHighlightUIGuide AddDiffHighlightUI(Action<ModuleDiffHighlightUIOption>? action = null)
        {
            return new ModuleDiffHighlightUIGuide().Register(action);
        }
    }
}

/// <summary>
/// 文本差异对比高亮UI模块
/// </summary>
public class ModuleDiffHighlightUI(ModuleDiffHighlightUIOption option)
    : MoModuleWithDependencies<ModuleDiffHighlightUI, ModuleDiffHighlightUIOption, ModuleDiffHighlightUIGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.DiffHighlightUI;
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
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UIDiffHighlightPage>(
                    UIDiffHighlightPage.DIFF_HIGHLIGHT_URL,
                    "Pages:DiffHighlight:Title",
                    Icons.Material.Filled.Compare,
                    "Categories:Debug",
                    addToNav: true,
                    navOrder: 60));
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