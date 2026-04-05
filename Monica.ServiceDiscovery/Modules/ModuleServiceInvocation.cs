using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.ServiceDiscovery.ServiceInvocation.Abstractions;
using Monica.ServiceDiscovery.ServiceInvocation.Providers;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Service call module
/// </summary>
[ModuleKey(BuiltInModuleKey.ServiceInvocation)]
public class ModuleServiceInvocation(ModuleServiceInvocationOption option)
    : ModuleBase<ModuleServiceInvocation, ModuleServiceInvocationOption, ModuleServiceInvocationGuide>(option)
{

    public override void ClaimDependencies()
    {
        // Service calling module has no dependencies
    }
}

/// <summary>
/// Service call module configuration options
/// </summary>
public class ModuleServiceInvocationOption : ModuleOptions<ModuleServiceInvocation>
{
    /// <summary>
    /// Whether to use a distributed call provider
    /// </summary>
    public bool UseDistributedProvider { get; internal set; }
}

/// <summary>
/// Service Call Module Configuration Guide
/// </summary>
public class ModuleServiceInvocationGuide : ModuleGuide<ModuleServiceInvocation, ModuleServiceInvocationOption, ModuleServiceInvocationGuide>
{
    private const string SET_PROVIDER = nameof(SET_PROVIDER);

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [SET_PROVIDER];
    }

    /// <summary>
    /// Use independent mode (service calling is not supported and an exception will be thrown when calling)
    /// </summary>
    public ModuleServiceInvocationGuide UseStandaloneProvider()
    {
        ConfigureEmpty(SET_PROVIDER);
        ConfigureModuleOption(o => o.UseDistributedProvider = false);
        ConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IServiceInvocationConnector, StandaloneServiceInvocationProvider>();
        });
        return this;
    }

    /// <summary>
    /// Using a distributed call provider
    /// </summary>
    /// <typeparam name="TProvider">provider type</typeparam>
    public ModuleServiceInvocationGuide UseDistributedProvider<TProvider>()
        where TProvider : class, IServiceInvocationConnector
    {
        ConfigureEmpty(SET_PROVIDER);
        ConfigureModuleOption(o => o.UseDistributedProvider = true);
        ConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IServiceInvocationConnector, TProvider>();
        });
        return this;
    }
}

public static class ModuleServiceInvocationBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the ServiceInvocation module
        /// </summary>
        public static ModuleServiceInvocationGuide AddServiceInvocation(Action<ModuleServiceInvocationOption>? action = null)
        {
            return new ModuleServiceInvocationGuide().Register(action);
        }
    }
}
