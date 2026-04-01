using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Dapr.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDaprRpcClientBuilderExtensions
{
    public static ModuleDaprRpcClientGuide UseDaprProvider(this ModuleRpcClientGuide guide,
        Action<ModuleDaprRpcClientOption>? action = null)
    {
        return new ModuleDaprRpcClientGuide().Register(action);
    }
}

[ModuleKey(EMoModuleKey.DaprRpcClient)]
public class ModuleDaprRpcClient(ModuleDaprRpcClientOption option)
    : MoModule<ModuleDaprRpcClient, ModuleDaprRpcClientOption, ModuleDaprRpcClientGuide>(option)
{

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleDaprClientGuide>().Register();
        DependsOnModule<ModuleRpcClientGuide>().Register()
            .ConfigHttpClientRegisterProvider<DaprRpcClientProvider>();
       
    }
}

public class ModuleDaprRpcClientGuide : MoModuleGuide<ModuleDaprRpcClient,
    ModuleDaprRpcClientOption, ModuleDaprRpcClientGuide>
{

}

public class ModuleDaprRpcClientOption : MoModuleOption<ModuleDaprRpcClient>
{
    /// <summary>
    /// Timeout applied to RPC calls.
    /// </summary>
    public TimeSpan Timeout { get; set; } =  TimeSpan.FromSeconds(60);
}
