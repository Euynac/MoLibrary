using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
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
        return new ModuleDaprServiceInvocationGuide().Register(action);
    }
}

/// <summary>
/// Dapr-based service invocation module.
/// </summary>
[ModuleKey(EMoModuleKey.DaprServiceInvocation)]
public class ModuleDaprServiceInvocation(ModuleDaprServiceInvocationOption option)
    : MoModule<ModuleDaprServiceInvocation, ModuleDaprServiceInvocationOption,
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

public class ModuleDaprServiceInvocationGuide : MoModuleGuide<ModuleDaprServiceInvocation,
    ModuleDaprServiceInvocationOption, ModuleDaprServiceInvocationGuide>
{
}

public class ModuleDaprServiceInvocationOption : MoModuleOption<ModuleDaprServiceInvocation>
{
}
