using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Modules;
using MoLibrary.RegisterCentre;

namespace MoLibrary.Configuration.Dashboard.Modules;

/// <summary>
/// Extension methods for registering and configuring the MoConfiguration Dashboard module
/// </summary>
public static class ModuleConfigurationDashboardBuilderExtensions
{
    /// <summary>
    /// Adds the MoConfiguration Dashboard module with default options
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>A configuration guide for further configuration</returns>
    public static ModuleConfigurationDashboardGuide AddMoModuleConfigurationDashboard(this IServiceCollection services)
    {
        return services.AddMoModule<ModuleConfigurationDashboard, ModuleConfigurationDashboardOption, ModuleConfigurationDashboardGuide>();
    }

    /// <summary>
    /// Adds the MoConfiguration Dashboard module with a custom dashboard implementation
    /// </summary>
    /// <typeparam name="TDashboard">Type of the dashboard implementation</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>A configuration guide for further configuration</returns>
    public static ModuleConfigurationDashboardGuide AddMoModuleConfigurationDashboard<TDashboard>(this IServiceCollection services)
        where TDashboard : class, IMoConfigurationDashboard
    {
        return services.AddMoModuleConfigurationDashboard()
            .UseDashboard<TDashboard>();
    }

    /// <summary>
    /// Adds the MoConfiguration Dashboard module as a client with custom connector and client types
    /// </summary>
    /// <typeparam name="TServer">Type of the register centre server connector</typeparam>
    /// <typeparam name="TClient">Type of the register centre client</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>A configuration guide for further configuration</returns>
    public static ModuleConfigurationDashboardGuide AddMoModuleConfigurationDashboardClient<TServer, TClient>(this IServiceCollection services)
        where TServer : class, IRegisterCentreServerConnector
        where TClient : class, IRegisterCentreClient
    {
        return services.AddMoModuleConfigurationDashboard()
            .AsClient<TServer, TClient>();
    }
} 