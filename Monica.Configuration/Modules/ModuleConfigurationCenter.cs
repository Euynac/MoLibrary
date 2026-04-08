using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Providers.ProjectCatalog;
using Monica.Configuration.Providers.ServiceInvocation;
using Monica.Configuration.Services;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleConfigurationCenterBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Enables configuration-center aggregation and remote update capabilities.
        /// </summary>
        public static ModuleConfigurationCenterGuide AddConfigurationCenter(Action<ModuleConfigurationCenterOption>? action = null)
        {
            return new ModuleConfigurationCenterGuide().Register(action);
        }
    }
}

/// <summary>
/// Configuration-center integration module.
/// Registers distributed configuration aggregation on top of the base configuration module.
/// </summary>
[ModuleKey(BuiltInModuleKey.ConfigurationCenter)]
public class ModuleConfigurationCenter(ModuleConfigurationCenterOption option)
    : ModuleBase<ModuleConfigurationCenter, ModuleConfigurationCenterOption, ModuleConfigurationCenterGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleConfigurationGuide>().Register();
        DependsOnModule<ModuleServiceDiscoveryGuide>().Register();
        DependsOnModule<ModuleServiceInvocationGuide>().Register();
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ConfigurationCenterManagementApi>();
        services.Replace(ServiceDescriptor.Singleton<IConfigurationManagementApi>(provider =>
            provider.GetRequiredService<ConfigurationCenterManagementApi>()));
        services.Replace(ServiceDescriptor.Singleton<IConfigurationProjectCatalog, ServiceDiscoveryConfigurationProjectCatalog>());
        services.Replace(ServiceDescriptor.Singleton<IConfigurationDashboardContext, ConfigurationCenterDashboardContext>());

        if (GetOptions<ModuleServiceDiscoveryOption>().IsStandaloneMode)
        {
            services.AddSingleton<IConfigurationRemoteGateway, StandaloneConfigurationRemoteGateway>();
            return;
        }

        services.AddSingleton<IConfigurationRemoteGateway, DistributedConfigurationRemoteGateway>();
    }
}

/// <summary>
/// Fluent guide for the configuration-center integration module.
/// </summary>
public class ModuleConfigurationCenterGuide
    : ModuleGuide<ModuleConfigurationCenter, ModuleConfigurationCenterOption, ModuleConfigurationCenterGuide>
{
}

/// <summary>
/// Options for the configuration-center integration module.
/// </summary>
public class ModuleConfigurationCenterOption : ModuleOptions<ModuleConfigurationCenter>
{
}
