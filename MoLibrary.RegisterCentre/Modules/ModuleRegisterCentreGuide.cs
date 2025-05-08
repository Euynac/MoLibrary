using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.RegisterCentre.Modules;

public class ModuleRegisterCentreGuide : MoModuleGuide<ModuleRegisterCentre, ModuleRegisterCentreOption, ModuleRegisterCentreGuide>
{
    public const string SET_CENTRE_TYPE = nameof(SET_CENTRE_TYPE);
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [SET_CENTRE_TYPE];
    }

    /// <summary>
    /// 注册MoRegisterCentreClient
    /// </summary>
    /// <returns></returns>
    public ModuleRegisterCentreGuide SetAsCentreServer()
    {
        ConfigureModuleOption(o =>
        {
            o.ThisIsCentreServer = true;
        });

        ConfigureExtraServices(SET_CENTRE_TYPE, context =>
        {
            context.Services.AddSingleton<IRegisterCentreServer, MemoryProviderForRegisterCentre>();
            context.Services.AddSingleton<IRegisterCentreClientConnector, DaprHttpForConnectClient>();
        });

        return this;
    }
    /// <summary>
    /// 注册MoRegisterCentreClient
    /// </summary>
    /// <returns></returns>
    public ModuleRegisterCentreGuide SetAsCentreClient<TServer, TClient>()
        where TServer : class, IRegisterCentreServerConnector where TClient : class, IRegisterCentreClient
    {
        ConfigureModuleOption(o =>
        {
            o.ThisIsCentreServer = true;
        });
        ConfigureExtraServices(SET_CENTRE_TYPE, context =>
        {
            context.Services.AddSingleton<IRegisterCentreServerConnector, TServer>();
            context.Services.AddSingleton<IRegisterCentreClient, TClient>();
        });
        return this;
    }
}