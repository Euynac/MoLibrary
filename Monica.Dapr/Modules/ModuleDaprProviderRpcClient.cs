using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.DomainDrivenDesign.Modules;

namespace Monica.Dapr.Modules;


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
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.DaprProviderRpcClient;
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
    public void ConfigureHttpClientBuilder(IHttpClientBuilder builder, string appid)
    {
        builder.ConfigureHttpClient(client =>
        {
            try
            {
                client.BaseAddress = new Uri($"http://{appid}");
            }
            catch (UriFormatException inner)
            {
                throw new ArgumentException("The appId must be a valid hostname.", nameof(appid), inner);
            }
            client.Timeout = option.Timeout;
        });
        builder.AddHttpMessageHandler(() => new InvocationHandler
        {
            DefaultAppId = appid
        });
    }
}
