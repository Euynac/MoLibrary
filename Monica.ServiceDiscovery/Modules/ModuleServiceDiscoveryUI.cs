using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.ServiceDiscovery.Localization;
using Monica.ServiceDiscovery.Pages;
using Monica.ServiceDiscovery.UIServiceDiscovery.State;
using Monica.ServiceDiscovery.UIServiceDiscovery.Support;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleServiceDiscoveryUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the Service Discovery monitoring page and its UI state.
        /// </summary>
        public ModuleRegistration<ModuleServiceDiscoveryUI, ModuleServiceDiscoveryUIOption> AddServiceDiscoveryUI(
            Action<ModuleServiceDiscoveryUIOption>? configure = null)
        {
            var registration = builder.AddModule<ModuleServiceDiscoveryUI, ModuleServiceDiscoveryUIOption>(configure);
            registration.Require<ModuleServiceDiscovery, ModuleServiceDiscoveryOption>();
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<ServiceDiscoveryResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(navigation =>
                    navigation.RegisterLocalizedPage<UIServiceDiscoveryPage, ServiceDiscoveryResource>(
                        UIServiceDiscoveryPage.SERVICE_DISCOVERY_DEBUG_URL,
                        "Pages:ServiceDiscovery:Title",
                        Icons.Material.Filled.CloudQueue,
                        BuiltInNavigationCategoryIds.Monitor,
                        addToNav: true,
                        navOrder: 40));
            return registration;
        }
    }
}

public class ModuleServiceDiscoveryUI : MonicaModule<ModuleServiceDiscoveryUIOption>, IUIModule
{
    public override void ConfigureServices(ModuleContext<ModuleServiceDiscoveryUIOption> context)
    {
        context.Services.AddScoped<ServiceDiscoveryDomainColorResolver>();
        context.Services.AddScoped<ServiceInstanceEvictionTracker>();
        context.Services.AddScoped<ServiceDiscoveryPageState>();
        context.Services.AddScoped<CurrentInstanceInfoState>();
    }
}

/// <summary>
/// Configures Service Discovery monitoring presentation.
/// </summary>
public class ModuleServiceDiscoveryUIOption : ModuleOptions<ModuleServiceDiscoveryUI>
{
    /// <summary>
    /// Gets metadata keys displayed directly in the service list.
    /// </summary>
    public List<string> DisplayMetadataKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets whether listening addresses are hidden from the service list.
    /// </summary>
    public bool DisableListeningAddressDisplay { get; set; }

    /// <summary>
    /// Gets or sets the maximum evicted instances retained per service.
    /// </summary>
    public int MaxEvictedServiceRetentionCount { get; set; } = 10;
}
