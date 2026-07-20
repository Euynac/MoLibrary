using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Dapr.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDaprRpcClientBuilderExtensions
{
    public static ModuleDaprRpcClientGuide UseDaprProvider(this ModuleRpcClientGuide guide,
        Action<ModuleDaprRpcClientOption>? action = null)
    {
        return guide.AddModule<ModuleDaprRpcClient, ModuleDaprRpcClientOption, ModuleDaprRpcClientGuide>(action);
    }
}

[ModuleKey(BuiltInModuleKey.DaprRpcClient)]
public class ModuleDaprRpcClient(ModuleDaprRpcClientOption option)
    : ModuleBase<ModuleDaprRpcClient, ModuleDaprRpcClientOption, ModuleDaprRpcClientGuide>(option)
{

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleDaprClientGuide>().Register();
        DependsOnModule<ModuleRpcClientGuide>().Register()
            .ConfigHttpClientRegisterProvider<DaprRpcClientProvider>();
       
    }
}

public class ModuleDaprRpcClientGuide : ModuleGuide<ModuleDaprRpcClient,
    ModuleDaprRpcClientOption, ModuleDaprRpcClientGuide>
{

}

public class ModuleDaprRpcClientOption : ModuleOptions<ModuleDaprRpcClient>
{
    /// <summary>
    /// Timeout applied to RPC calls.
    /// </summary>
    public TimeSpan Timeout { get; set; } =  TimeSpan.FromSeconds(60);
}
