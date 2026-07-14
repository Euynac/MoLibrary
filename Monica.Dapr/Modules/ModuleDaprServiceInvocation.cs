using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Dapr.Services;
using Monica.ServiceDiscovery.ServiceInvocation.Abstractions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDaprServiceInvocationBuilderExtensions
{
    /// <summary>
    /// Registers Dapr as the service invocation provider.
    /// </summary>
    public static ModuleDaprServiceInvocationGuide UseDaprInvocationProvider(
        this ModuleServiceInvocationGuide guide, Action<ModuleDaprServiceInvocationOption>? action = null)
    {
        guide.UseDistributedProvider<DaprServiceInvocationConnector>();
        return guide.AddModule<ModuleDaprServiceInvocation, ModuleDaprServiceInvocationOption, ModuleDaprServiceInvocationGuide>(action);
    }
}

/// <summary>
/// Dapr-based service invocation module.
/// </summary>
[ModuleKey(BuiltInModuleKey.DaprServiceInvocation)]
public class ModuleDaprServiceInvocation(ModuleDaprServiceInvocationOption option)
    : ModuleBase<ModuleDaprServiceInvocation, ModuleDaprServiceInvocationOption,
        ModuleDaprServiceInvocationGuide>(option)
{

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleJsonSerializationGuide>().Register();
        DependsOnModule<ModuleDaprClientGuide>().Register();
        DependsOnModule<ModuleServiceInvocationGuide>().Register();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IServiceInvocationConnector, DaprServiceInvocationConnector>();
    }
}

public class ModuleDaprServiceInvocationGuide : ModuleGuide<ModuleDaprServiceInvocation,
    ModuleDaprServiceInvocationOption, ModuleDaprServiceInvocationGuide>
{
}

public class ModuleDaprServiceInvocationOption : ModuleOptions<ModuleDaprServiceInvocation>
{
}
