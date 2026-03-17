using Dapr.Client;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;


public static class ModuleDaprProviderRpcClientBuilderExtensions
{
    public static ModuleDaprProviderRpcClientGuide UseDaprProvider(this ModuleRpcClientGuide guide,
        Action<ModuleDaprProviderRpcClientOption>? action = null)
    {
        return new ModuleDaprProviderRpcClientGuide().Register(action);
    }
}

public class ModuleDaprProviderRpcClient(ModuleDaprProviderRpcClientOption option)
    : MoModule<ModuleDaprProviderRpcClient, ModuleDaprProviderRpcClientOption, ModuleDaprProviderRpcClientGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.DaprProviderRpcClient;
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleDaprClientGuide>().Register();
        DependsOnModule<ModuleRpcClientGuide>().Register()
            .ConfigHttpClientRegisterProvider<DaprHttpClientRegisterProvider>();
       
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

public class DaprHttpClientRegisterProvider(
    IOptions<ModuleDaprProviderRpcClientOption> rpcClientOptionAccessor,
    IOptions<ModuleDaprClientOption> daprClientOptionAccessor) : IMoRpcHttpClientRegisterProvider
{
    public void ConfigureHttpClientFactoryOptions(HttpClientFactoryOptions options, string appid)
    {
        options.HttpClientActions.Add(client =>
        {
            try
            {
                client.BaseAddress = new Uri($"http://{appid}");
            }
            catch (UriFormatException inner)
            {
                throw new ArgumentException("The appId must be a valid hostname.", nameof(appid), inner);
            }
            client.Timeout = rpcClientOptionAccessor.Value.Timeout;
        });

        if (daprClientOptionAccessor.Value.MaxReceiveMessageSize is { } size)
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builder.PrimaryHandler = new SocketsHttpHandler
                {
                    MaxResponseHeadersLength = size / 1024, // Convert to KB
                };
            });
        }

        options.HttpMessageHandlerBuilderActions.Add(builder =>
        {
            builder.AdditionalHandlers.Add(new InvocationHandler
            {
                DefaultAppId = appid
            });
        });
    }
}
