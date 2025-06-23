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
    private bool? _isDashboard;

    /// <summary>
    /// 注册默认MoConfigurationDashboard
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public ModuleConfigurationDashboardGuide AddMoConfigurationDashboard()
    {
        AddMoConfigurationDashboard<DefaultArrangeDashboard>();
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
        if (_isDashboard is false) throw new InvalidOperationException("已设置为非面板服务，无法注册面板服务");
        _isDashboard = true;
        ConfigureModuleOption(o => { o.ThisIsDashboard = true; });
        DependsOnModule<ModuleRegisterCentreGuide>().Register().SetAsCentreServer();
        ConfigureServices(context =>
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

    /// <summary>
    /// 注册MoConfigurationDashboard仓储
    /// </summary>
    /// <typeparam name="TStore">面板仓储接口</typeparam>
    /// <returns></returns>
    public ModuleConfigurationDashboardGuide AddMoConfigurationDashboardStore<TStore>()
        where TStore : class, IMoConfigurationStores
    {
        if (_isDashboard is false)
        {
            throw new InvalidOperationException("非面板服务无需注册面板仓储接口");
        }
        _isDashboard = true;
        ConfigureServices(context => { context.Services.Replace(ServiceDescriptor.Transient<IMoConfigurationStores, TStore>()); });
        return this;
    }

    /// <summary>
    /// 注册MoConfigurationDashboard客户端
    /// </summary>
    /// <typeparam name="TServer">注册中心服务器接口</typeparam>
    /// <typeparam name="TClient">注册中心客户端接口</typeparam>
    /// <param name="action">可选的配置操作</param>
    public ModuleConfigurationDashboardGuide AddMoConfigurationDashboardClient<TServer, TClient>(
        Action<ModuleRegisterCentreOption>? action = null)
        where TServer : class, IRegisterCentreServerConnector
        where TClient : class, IRegisterCentreClient
    {
        if (_isDashboard is true) throw new InvalidOperationException("面板服务无需注册面板客户端");
        _isDashboard = false;
        DependsOnModule<ModuleRegisterCentreGuide>().Register(action).SetAsCentreClient<TServer, TClient>();
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IMoConfigurationModifier, MoConfigurationJsonFileModifier>();
        });
        return this;
    }


}