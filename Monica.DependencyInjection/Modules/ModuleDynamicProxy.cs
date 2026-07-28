using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.DependencyInjection.DynamicProxy.Abstractions;
using Monica.DependencyInjection.DynamicProxy.Models;
using Monica.DependencyInjection.DynamicProxy.Models.Internal;
using Monica.DependencyInjection.DynamicProxy.Providers.Castle;
using Monica.DependencyInjection.DynamicProxy.Services.Support;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDynamicProxyBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the DynamicProxy module.
        /// </summary>
        public ModuleDynamicProxyGuide AddDynamicProxy(Action<ModuleDynamicProxyOption>? action = null)
        {
            return builder.AddModule<ModuleDynamicProxy, ModuleDynamicProxyOption, ModuleDynamicProxyGuide>(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.DynamicProxy)]
public class ModuleDynamicProxy(ModuleDynamicProxyOption option)
    : ModuleBase<ModuleDynamicProxy, ModuleDynamicProxyOption, ModuleDynamicProxyGuide>(option)
{
    public override void PostConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(new ServiceProviderProxyGenerator());
        services.AddTransient(typeof(AsyncDeterminationInterceptorAdapter<>));
        DynamicProxyServiceRegistrar.ApplyInterceptors(services, Option);
    }

    protected override int GetPostConfigureServicesOrder()
    {
        return (int)ModuleRegistrationOrder.PostConfig;
    }
}

public class
    ModuleDynamicProxyGuide : ModuleGuide<ModuleDynamicProxy, ModuleDynamicProxyOption, ModuleDynamicProxyGuide>
{
    /// <summary>
    /// Registers a dynamic-proxy interceptor through the module guide.
    /// </summary>
    /// <typeparam name="TInterceptor">The interceptor type.</typeparam>
    /// <param name="shouldIntercept">Predicate that decides whether the interceptor applies to a service.</param>
    /// <param name="secondKey">Optional stable secondary key used when repeated calls should collapse into a single configuration.</param>
    /// <remarks>
    /// Monica uses an interface proxy when the exposed service type is an interface and a class proxy otherwise, unless
    /// the host configures another proxy kind explicitly. Class proxies require a non-sealed implementation, and only
    /// virtual members can be intercepted. A sealed implementation can still be intercepted when it is exposed through
    /// an interface and uses <see cref="EDynamicProxyKind.InterfaceProxy"/>.
    /// </remarks>
    public ModuleDynamicProxyGuide AddInterceptor<TInterceptor>(
        Func<ProxyBuildContext, bool> shouldIntercept,
        string? secondKey = null)
        where TInterceptor : InvocationInterceptor
    {
        var interceptorKey = secondKey ?? Guid.NewGuid().ToString();

        ConfigureModuleOption(option => option.AddInterceptor<TInterceptor>(shouldIntercept), secondKey: interceptorKey);
        ConfigureServices(context => { context.Services.TryAddTransient<TInterceptor>(); }, secondKey: interceptorKey);

        return this;
    }

    /// <summary>
    /// Configures the proxy kind for a specific service type.
    /// </summary>
    /// <typeparam name="TServiceType">The service type to configure.</typeparam>
    /// <param name="kind">The proxy kind.</param>
    /// <remarks>
    /// <see cref="EDynamicProxyKind.InterfaceProxy"/> requires <typeparamref name="TServiceType"/> to be an interface.
    /// <see cref="EDynamicProxyKind.ClassProxy"/> requires each matched implementation to be non-sealed, and only its
    /// virtual members can be intercepted.
    /// </remarks>
    public ModuleDynamicProxyGuide SetProxyKindOfServiceType<TServiceType>(EDynamicProxyKind kind)
    {
        ConfigureModuleOption(option => option.SetProxyKindOfServiceType<TServiceType>(kind),
            secondKey: typeof(TServiceType).FullName);
        return this;
    }

}
public class ModuleDynamicProxyOption : ModuleOptions<ModuleDynamicProxy>
{
    /// <summary>
    /// Configured proxy kinds for specific types.
    /// </summary>
    public Dictionary<Type, EDynamicProxyKind> ConfiguredProxyKinds { get; internal set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to log a warning when a service registered as a factory or instance
    /// is proxied with a class proxy.
    /// </summary>
    /// <remarks>
    /// Class-proxy-with-target keeps separate field state on the proxy and the target instance, which can diverge for
    /// services that hold mutable instance state. This warning is a generic caution: it also fires for the common
    /// Monica pattern where services implement <c>ICachedServiceProviderAccessor</c> and are therefore registered as a
    /// factory by convention, even though those services are stateless and safe. Disabled by default to avoid startup
    /// noise; enable it when diagnosing a class proxy on a genuinely stateful service.
    /// </remarks>
    public bool EnableClassProxyTargetStateWarning { get; set; }

    internal List<DynamicProxyInterceptorRegistration> InterceptorRegistrations { get; } = [];

    /// <summary>
    /// Sets the proxy kind for a specific service type.
    /// </summary>
    /// <typeparam name="TServiceType">The service type.</typeparam>
    /// <param name="kind">The kind of dynamic proxy.</param>
    public void SetProxyKindOfServiceType<TServiceType>(EDynamicProxyKind kind)
    {
        var serviceType = typeof(TServiceType);
        ConfiguredProxyKinds.Add(serviceType, kind);
    }

    internal void AddInterceptor<TInterceptor>(
        Func<ProxyBuildContext, bool> shouldIntercept)
        where TInterceptor : InvocationInterceptor
    {
        InterceptorRegistrations.Add(new DynamicProxyInterceptorRegistration(typeof(TInterceptor), shouldIntercept));
    }
}
