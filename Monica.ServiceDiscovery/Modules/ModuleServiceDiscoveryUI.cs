using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.ServiceDiscovery.Pages;
using Monica.ServiceDiscovery.UIServiceDiscovery.State;
using Monica.ServiceDiscovery.UIServiceDiscovery.Support;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

[ModuleKey(EMoModuleKey.ServiceDiscoveryUI)]
public class ModuleServiceDiscoveryUI(ModuleServiceDiscoveryUIOption option)
    : MoModule<ModuleServiceDiscoveryUI, ModuleServiceDiscoveryUIOption, ModuleServiceDiscoveryUIGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ServiceDiscoveryDomainColorResolver>();
        services.AddScoped<ServiceInstanceEvictionTracker>();
        services.AddScoped<ServiceDiscoveryPageState>();
        services.AddScoped<CurrentInstanceInfoState>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableServiceDiscoveryPage)
        {
            DependsOnModule<ModuleServiceDiscoveryGuide>().Register();
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UIServiceDiscoveryPage>(
                    UIServiceDiscoveryPage.SERVICE_DISCOVERY_DEBUG_URL,
                    "Pages:ServiceDiscovery:Title",
                    Icons.Material.Filled.CloudQueue,
                    "Categories:Monitor",
                    addToNav: true,
                    navOrder: 40));
        }
    }
}

public class ModuleServiceDiscoveryUIGuide : MoModuleGuide<ModuleServiceDiscoveryUI, ModuleServiceDiscoveryUIOption, ModuleServiceDiscoveryUIGuide>
{
}

public static class ModuleServiceDiscoveryUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 ServiceDiscoveryUI 模块
        /// </summary>
        public static ModuleServiceDiscoveryUIGuide AddServiceDiscoveryUI(Action<ModuleServiceDiscoveryUIOption>? action = null)
        {
            return new ModuleServiceDiscoveryUIGuide().Register(action);
        }
    }
}

public class ModuleServiceDiscoveryUIOption : MoModuleOption<ModuleServiceDiscoveryUI>
{ 
    public bool DisableServiceDiscoveryPage { get; set; }
    
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
