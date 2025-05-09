using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.RegisterCentre;
using MoLibrary.RegisterCentre.Modules;

namespace MoLibrary.Configuration.Dashboard.Modules;

public class ModuleConfigurationDashboardGuide : MoModuleGuide<ModuleConfigurationDashboard,
    ModuleConfigurationDashboardOption, ModuleConfigurationDashboardGuide>
{
    private bool _isDashboard;

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(AddMoConfigurationDashboard)];
    }


    /// <summary>
    /// 注册默认MoConfigurationDashboard
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public ModuleConfigurationDashboardGuide AddMoConfigurationDashboard()
    {
        _isDashboard = true;
        ConfigureModuleOption(o => { o.ThisIsDashboard = true; });
        ConfigureServices(nameof(AddMoConfigurationDashboard),
            context => { context.Services.AddMoConfigurationDashboard<DefaultArrangeDashboard>(); });
        return this;
    }

    /// <summary>
    /// 注册MoConfigurationDashboard
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public ModuleConfigurationDashboardGuide AddMoConfigurationDashboard<TDashboard>()
        where TDashboard : class, IMoConfigurationDashboard
    {
        _isDashboard = true;
        ConfigureModuleOption(o => { o.ThisIsDashboard = true; });
        ConfigureServices(nameof(AddMoConfigurationDashboard), context =>
        {
            context.Services.AddSingleton<IMoConfigurationDashboard, TDashboard>();
            context.Services.AddSingleton<MemoryProviderForConfigCentre>();
            context.Services.AddSingleton(p =>
                ((IRegisterCentreServer?) p.GetService(typeof(MemoryProviderForConfigCentre)))!);

            context.Services.AddSingleton(p =>
                ((IMoConfigurationCentre?) p.GetService(typeof(MemoryProviderForConfigCentre)))!);
            MoConfigurationManager.Setting.ThisIsDashboard = true;
            context.Services.AddSingleton<IMoConfigurationStores, MoConfigurationDefaultStore>();
            context.Services.AddSingleton<IMoConfigurationModifier, MoConfigurationJsonFileModifier>();
        });
        return this;
    }

    public ModuleConfigurationDashboardGuide AddMoConfigurationDashboardStore<TStore>()
        where TStore : class, IMoConfigurationStores
    {
        if (!_isDashboard)
        {
            throw new InvalidOperationException("非面板服务无需注册面板仓储接口");
        }

        ConfigureServices(nameof(AddMoConfigurationDashboardStore),
            context => { context.Services.Replace(ServiceDescriptor.Transient<IMoConfigurationStores, TStore>()); });
        return this;
    }

    public ModuleConfigurationDashboardGuide AddMoConfigurationDashboardClient<TServer, TClient>(
        Action<ModuleRegisterCentreOption>? action = null)
        where TServer : class, IRegisterCentreServerConnector
        where TClient : class, IRegisterCentreClient
    {
        if (_isDashboard) throw new InvalidOperationException("面板服务无需注册面板客户端");
        DependsOnModule<ModuleRegisterCentreGuide>().Register().SetAsCentreClient<TServer, TClient>();
        ConfigureServices(nameof(AddMoConfigurationDashboardClient), context =>
        {
            context.Services.AddSingleton<IMoConfigurationModifier, MoConfigurationJsonFileModifier>();
        });
        return this;
    }


}