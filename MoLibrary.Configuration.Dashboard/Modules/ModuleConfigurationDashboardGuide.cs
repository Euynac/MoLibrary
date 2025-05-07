using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Modules;
using MoLibrary.RegisterCentre;

namespace MoLibrary.Configuration.Dashboard.Modules;

/// <summary>
/// Guide for configuring the MoConfiguration Dashboard module
/// </summary>
public class ModuleConfigurationDashboardGuide : ModuleConfigurationGuide<ModuleConfigurationDashboard, ModuleConfigurationDashboardOption>
{
    /// <summary>
    /// Sets the dashboard implementation type
    /// </summary>
    /// <typeparam name="TDashboard">Type of dashboard implementation</typeparam>
    /// <returns>The configuration guide for fluent chaining</returns>
    public ModuleConfigurationDashboardGuide UseDashboard<TDashboard>()
        where TDashboard : class, IMoConfigurationDashboard
    {
        ConfigureExtraServices(nameof(UseDashboard), ctx =>
        {
            Option.DashboardImplementationType = typeof(TDashboard);
        });
        return this;
    }

    /// <summary>
    /// Sets the configuration store type
    /// </summary>
    /// <typeparam name="TStore">Type of configuration store</typeparam>
    /// <returns>The configuration guide for fluent chaining</returns>
    public ModuleConfigurationDashboardGuide UseStore<TStore>()
        where TStore : class, IMoConfigurationStores
    {
        ConfigureExtraServices(nameof(UseStore), ctx =>
        {
            Option.ConfigurationStoreType = typeof(TStore);
        });
        return this;
    }

    /// <summary>
    /// Configures the module as a client for connecting to a dashboard
    /// </summary>
    /// <typeparam name="TServer">Type of register centre server connector</typeparam>
    /// <typeparam name="TClient">Type of register centre client</typeparam>
    /// <returns>The configuration guide for fluent chaining</returns>
    public ModuleConfigurationDashboardGuide AsClient<TServer, TClient>()
        where TServer : class, IRegisterCentreServerConnector
        where TClient : class, IRegisterCentreClient
    {
        ConfigureExtraServices(nameof(AsClient), ctx =>
        {
            Option.IsClient = true;
            Option.RegisterCentreServerConnectorType = typeof(TServer);
            Option.RegisterCentreClientType = typeof(TClient);
        });
        return this;
    }

    /// <summary>
    /// Sets the group name for OpenAPI documentation
    /// </summary>
    /// <param name="groupName">The group name to use</param>
    /// <returns>The configuration guide for fluent chaining</returns>
    public ModuleConfigurationDashboardGuide SetOpenApiGroupName(string groupName)
    {
        ConfigureExtraServices(nameof(SetOpenApiGroupName), ctx =>
        {
            Option.OpenApiGroupName = groupName;
        });
        return this;
    }
} 