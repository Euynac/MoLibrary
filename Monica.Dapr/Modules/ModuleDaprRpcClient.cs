using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Dapr.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDaprRpcClientBuilderExtensions
{
    public static ModuleRegistration<ModuleDaprRpcClient, ModuleDaprRpcClientOption> UseDaprProvider(
        this ModuleRegistration<ModuleRpcClient, ModuleRpcClientOption> module,
        Action<ModuleDaprRpcClientOption>? action = null)
    {
        module.ConfigHttpClientRegisterProvider<DaprRpcClientProvider>();
        return module.Include<ModuleDaprRpcClient, ModuleDaprRpcClientOption>(action);
    }
}

public class ModuleDaprRpcClient : MonicaModule<ModuleDaprRpcClientOption>
{

    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleDaprClient, ModuleDaprClientOption>();
        module.Require<ModuleRpcClient, ModuleRpcClientOption>();
    }
}



public class ModuleDaprRpcClientOption : ModuleOptions<ModuleDaprRpcClient>
{
    /// <summary>
    /// Timeout applied to RPC calls.
    /// </summary>
    public TimeSpan Timeout { get; set; } =  TimeSpan.FromSeconds(60);
}
