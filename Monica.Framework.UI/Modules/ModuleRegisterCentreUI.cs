using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Framework.UI.UIRegisterCentre.Services;
using Monica.Framework.UI.Pages;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public class ModuleRegisterCentreUI(ModuleRegisterCentreUIOption option)
    : MoModuleWithDependencies<ModuleRegisterCentreUI, ModuleRegisterCentreUIOption, ModuleRegisterCentreUIGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.RegisterCentreUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<RegisterCentreService>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableRegisterCentrePage)
        {
            DependsOnModule<ModuleRegisterCentreGuide>().Register();
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UIRegisterCentrePage>(
                    UIRegisterCentrePage.REGISTERCENTRE_DEBUG_URL,
                    "Pages:RegisterCentre:Title",
                    Icons.Material.Filled.CloudQueue,
                    "Categories:Monitor",
                    addToNav: true,
                    navOrder: 40));
        }
    }
}

public class ModuleRegisterCentreUIGuide : MoModuleGuide<ModuleRegisterCentreUI, ModuleRegisterCentreUIOption, ModuleRegisterCentreUIGuide>
{
}

public static class ModuleRegisterCentreUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 RegisterCentreUI 模块
        /// </summary>
        public static ModuleRegisterCentreUIGuide AddRegisterCentreUI(Action<ModuleRegisterCentreUIOption>? action = null)
        {
            return new ModuleRegisterCentreUIGuide().Register(action);
        }
    }
}

public class ModuleRegisterCentreUIOption : MoModuleOption<ModuleRegisterCentreUI>
{ 
    public bool DisableRegisterCentrePage { get; set; }
    
    /// <summary>
    /// 需要在列表界面直接展示的元数据Key列表
    /// </summary>
    public List<string> DisplayMetadataKeys { get; set; } = [];

    /// <summary>
    /// 是否禁用列表界面展示监听地址
    /// </summary>
    public bool DisableListeningAddressDisplay { get; set; }

    /// <summary>
    /// Maximum number of evicted instances to retain per service for history tracking.
    /// Default: 10
    /// </summary>
    public int MaxEvictedServiceRetentionCount { get; set; } = 10;
}