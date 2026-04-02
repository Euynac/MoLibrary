using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
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
    extension(Mo)
    {
        /// <summary>
        /// Configures the DynamicProxy module.
        /// </summary>
        public static ModuleDynamicProxyGuide AddDynamicProxy(Action<ModuleDynamicProxyOption>? action = null)
        {
            return new ModuleDynamicProxyGuide().Register(action).EnsureCoreServices();
        }
    }
}

[ModuleKey(EMoModuleKey.DynamicProxy)]
public class ModuleDynamicProxy(ModuleDynamicProxyOption option)
    : MoModule<ModuleDynamicProxy, ModuleDynamicProxyOption, ModuleDynamicProxyGuide>(option)
{
}

public class
    ModuleDynamicProxyGuide : MoModuleGuide<ModuleDynamicProxy, ModuleDynamicProxyOption, ModuleDynamicProxyGuide>
{
    private const string CONFIG_CORE_SERVICES = nameof(CONFIG_CORE_SERVICES);

    internal ModuleDynamicProxyGuide EnsureCoreServices()
    {
        PostConfigureServices(context =>
        {
            context.Services.AddSingleton(new ServiceProviderProxyGenerator());
            context.Services.AddTransient(typeof(AsyncDeterminationInterceptorAdapter<>));
            DynamicProxyServiceRegistrar.ApplyInterceptors(context.Services,
                context.ModuleOption);
        }, EMoModuleOrder.PostConfig, key: CONFIG_CORE_SERVICES);
        return this;
    }

    /// <summary>
    /// Registers a dynamic-proxy interceptor through the module guide.
    /// </summary>
    /// <typeparam name="TInterceptor">The interceptor type.</typeparam>
    /// <param name="shouldIntercept">Predicate that decides whether the interceptor applies to a service.</param>
    /// <param name="secondKey">Optional stable secondary key used when repeated calls should collapse into a single configuration.</param>
    public ModuleDynamicProxyGuide AddInterceptor<TInterceptor>(
        Func<ProxyBuildContext, bool> shouldIntercept,
        string? secondKey = null)
        where TInterceptor : InvocationInterceptor
    {
        EnsureCoreServices();

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
    public ModuleDynamicProxyGuide SetProxyKindOfServiceType<TServiceType>(EDynamicProxyKind kind)
    {
        EnsureCoreServices();
        ConfigureModuleOption(option => option.SetProxyKindOfServiceType<TServiceType>(kind),
            secondKey: typeof(TServiceType).FullName);
        return this;
    }
}

public class ModuleDynamicProxyOption : MoModuleOption<ModuleDynamicProxy>
{
    /// <summary>
    /// Configured proxy kinds for specific types.
    /// </summary>
    public Dictionary<Type, EDynamicProxyKind> ConfiguredProxyKinds { get; internal set; } = new();

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
