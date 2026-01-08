using Dapr.Client;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.DomainDrivenDesign.Modules;

namespace MoLibrary.Dapr.Modules;


public static class ModuleDaprProviderRpcClientBuilderExtensions
{
    public static ModuleDaprProviderRpcClientGuide UseDaprProvider(this ModuleRpcClientGuide guide,
        Action<ModuleDaprProviderRpcClientOption>? action = null)
    {
        return new ModuleDaprProviderRpcClientGuide().Register(action);
    }
}

public class ModuleDaprProviderRpcClient(ModuleDaprProviderRpcClientOption option)
    : MoModuleWithDependencies<ModuleDaprProviderRpcClient, ModuleDaprProviderRpcClientOption, ModuleDaprProviderRpcClientGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DaprProviderRpcClient;
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleRpcClientGuide>().Register()
            .ConfigHttpClientRegisterProvider(new DaprHttpClientRegisterProvider(Option));
    }
}

public class ModuleDaprProviderRpcClientGuide : MoModuleGuide<ModuleDaprProviderRpcClient,
    ModuleDaprProviderRpcClientOption, ModuleDaprProviderRpcClientGuide>
{


}

public class ModuleDaprProviderRpcClientOption : MoModuleOption<ModuleDaprProviderRpcClient>
{
    /// <summary>
    /// RPC接口调用超时时间
    /// </summary>
    public TimeSpan Timeout { get; set; } =  TimeSpan.FromSeconds(60);
}

public class DaprHttpClientRegisterProvider(ModuleDaprProviderRpcClientOption option) : IMoRpcHttpClientRegisterProvider
{
    public HttpClient GetHttpClient(string appid)
    {
        var client = DaprClient.CreateInvokeHttpClient(appid);
        client.Timeout = option.Timeout;
        return client;
    }
}
