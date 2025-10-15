using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.DomainDrivenDesign.Modules;

namespace MoLibrary.Dapr.Modules;


public static class ModuleRpcClientDaprProviderBuilderExtensions
{
    public static ModuleRpcClientDaprProviderGuide ConfigModuleRpcClientDaprProvider(this WebApplicationBuilder builder,
        Action<ModuleRpcClientDaprProviderOption>? action = null)
    {
        return new ModuleRpcClientDaprProviderGuide().Register(action);
    }
}

public class ModuleRpcClientDaprProvider(ModuleRpcClientDaprProviderOption option)
    : MoModuleWithDependencies<ModuleRpcClientDaprProvider, ModuleRpcClientDaprProviderOption, ModuleRpcClientDaprProviderGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.RpcClientDaprProvider;
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleRpcClientGuide>().Register()
            .ConfigHttpClientRegisterProvider(new DaprHttpClientRegisterProvider(Option));
    }
}

public class ModuleRpcClientDaprProviderGuide : MoModuleGuide<ModuleRpcClientDaprProvider,
    ModuleRpcClientDaprProviderOption, ModuleRpcClientDaprProviderGuide>
{


}

public class ModuleRpcClientDaprProviderOption : MoModuleOption<ModuleRpcClientDaprProvider>
{
    /// <summary>
    /// RPC接口调用超时时间
    /// </summary>
    public TimeSpan Timeout { get; set; } =  TimeSpan.FromSeconds(60);
}

public class DaprHttpClientRegisterProvider(ModuleRpcClientDaprProviderOption option) : IMoRpcHttpClientRegisterProvider
{
    public HttpClient GetHttpClient(string appid)
    {
        var client = DaprClient.CreateInvokeHttpClient(appid);
        client.Timeout = option.Timeout;
        return client;
    }
}
