using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
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
    public static ModuleRegistration<ModuleDaprServiceInvocation, ModuleDaprServiceInvocationOption> UseDaprInvocationProvider(
        this ModuleRegistration<ModuleServiceInvocation, ModuleServiceInvocationOption> module,
        Action<ModuleDaprServiceInvocationOption>? action = null)
    {
        module.UseDistributedProvider<DaprServiceInvocationConnector>();
        return module.Include<ModuleDaprServiceInvocation, ModuleDaprServiceInvocationOption>(action);
    }
}

/// <summary>
/// Dapr-based service invocation module.
/// </summary>
public class ModuleDaprServiceInvocation : MonicaModule<ModuleDaprServiceInvocationOption>
{

    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleJsonSerialization, ModuleJsonSerializationOption>();
        module.Require<ModuleDaprClient, ModuleDaprClientOption>();
        module.Require<ModuleServiceInvocation, ModuleServiceInvocationOption>();
    }

    public override void ConfigureServices(ModuleContext<ModuleDaprServiceInvocationOption> context)
    {
        var services = context.Services;
        services.AddHttpClient(DaprServiceInvocationConnector.HttpClientName);
        services.AddSingleton<IServiceInvocationConnector, DaprServiceInvocationConnector>();
    }
}



public class ModuleDaprServiceInvocationOption : ModuleOptions<ModuleDaprServiceInvocation>
{
}
