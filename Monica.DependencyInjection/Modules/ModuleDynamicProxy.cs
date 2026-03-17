using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DependencyInjection.DynamicProxy;
using Monica.DependencyInjection.DynamicProxy.Abstract;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDynamicProxyBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 DynamicProxy 模块
        /// </summary>
        public static ModuleDynamicProxyGuide AddDynamicProxy(Action<ModuleDynamicProxyOption>? action = null)
        {
            return new ModuleDynamicProxyGuide().Register(action).ConfigDynamicProxyServices();
        }
    }
}

public class ModuleDynamicProxy(ModuleDynamicProxyOption option)
    : MoModule<ModuleDynamicProxy, ModuleDynamicProxyOption, ModuleDynamicProxyGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.DynamicProxy;
    }
}

public class
    ModuleDynamicProxyGuide : MoModuleGuide<ModuleDynamicProxy, ModuleDynamicProxyOption, ModuleDynamicProxyGuide>
{

    internal ModuleDynamicProxyGuide ConfigDynamicProxyServices()
    {
        PostConfigureServices(context =>
        {
            context.Services.AddSingleton(new ProxyGeneratorWithDI());
            context.Services.AddTransient(typeof(MoAsyncDeterminationInterceptor<>));
            MicrosoftDependencyInjectionDynamicProxyExtensions.ApplyInterceptors(context.Services,
                context.ModuleOption);
        }, EMoModuleOrder.PostConfig);
        return this;
    }
}

public class ModuleDynamicProxyOption : MoModuleOption<ModuleDynamicProxy>
{
    /// <summary>
    /// Configured proxy kinds for specific types.
    /// </summary>
    public Dictionary<Type, EDynamicProxyKind> ConfiguredProxyKinds { get; internal set; } = new();
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
}
