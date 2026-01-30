using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.DependencyInjection.DynamicProxy;
using Monica.DependencyInjection.DynamicProxy.Abstract;

namespace Monica.DependencyInjection.Modules;

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
