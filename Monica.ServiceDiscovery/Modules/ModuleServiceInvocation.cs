using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.ServiceDiscovery.ServiceInvocation.Abstractions;
using Monica.ServiceDiscovery.ServiceInvocation.Providers;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleServiceInvocationBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the service-invocation abstraction for the current host.
        /// </summary>
        public ModuleRegistration<ModuleServiceInvocation, ModuleServiceInvocationOption> AddServiceInvocation(
            Action<ModuleServiceInvocationOption>? configure = null)
        {
            return builder.AddModule<ModuleServiceInvocation, ModuleServiceInvocationOption>(configure);
        }
    }

    extension(ModuleRegistration<ModuleServiceInvocation, ModuleServiceInvocationOption> registration)
    {
        /// <summary>
        /// Selects standalone mode, in which remote invocation is rejected explicitly.
        /// </summary>
        public ModuleRegistration<ModuleServiceInvocation, ModuleServiceInvocationOption> UseStandaloneProvider()
        {
            return registration
                .Configure(options =>
                    options.SetProvider<StandaloneServiceInvocationProvider>(useDistributedProvider: false))
                .SatisfyFeature(ModuleServiceInvocation.PROVIDER_FEATURE);
        }

        /// <summary>
        /// Selects a distributed service-invocation connector.
        /// </summary>
        public ModuleRegistration<ModuleServiceInvocation, ModuleServiceInvocationOption> UseDistributedProvider<TProvider>()
            where TProvider : class, IServiceInvocationConnector
        {
            return registration
                .Configure(options => options.SetProvider<TProvider>(useDistributedProvider: true))
                .SatisfyFeature(ModuleServiceInvocation.PROVIDER_FEATURE);
        }
    }
}

/// <summary>
/// Registers one explicitly selected service-invocation connector.
/// </summary>
public class ModuleServiceInvocation : MonicaModule<ModuleServiceInvocationOption>
{
    internal const string PROVIDER_FEATURE = "service-invocation-provider";

    public override void Describe(ModuleDescriptor module)
    {
        module.RequireFeature(PROVIDER_FEATURE);
    }

    public override void ConfigureServices(ModuleContext<ModuleServiceInvocationOption> context)
    {
        context.Services.TryAdd(ServiceDescriptor.Singleton(
            typeof(IServiceInvocationConnector),
            Option.ProviderType));
    }
}

/// <summary>
/// Configures the selected service-invocation strategy.
/// </summary>
public class ModuleServiceInvocationOption : ModuleOptions<ModuleServiceInvocation>
{
    private Type? _providerType;

    /// <summary>
    /// Gets whether the selected connector performs distributed calls.
    /// </summary>
    public bool UseDistributedProvider { get; private set; }

    internal Type ProviderType => _providerType
        ?? throw new InvalidOperationException("A service-invocation provider was not selected.");

    internal void SetProvider<TProvider>(bool useDistributedProvider)
        where TProvider : class, IServiceInvocationConnector
    {
        _providerType = typeof(TProvider);
        UseDistributedProvider = useDistributedProvider;
    }
}
