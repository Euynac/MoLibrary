using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.DependencyInjection.DynamicProxy.Abstract;

namespace MoLibrary.DependencyInjection.Modules;

public static class ModuleDynamicProxyBuilderExtensions
{
    public static ModuleDynamicProxyGuide ConfigModuleDynamicProxy(this WebApplicationBuilder builder,
        Action<ModuleDynamicProxyOption>? action = null)
    {
        return new ModuleDynamicProxyGuide().Register(action).ConfigDynamicProxyServices();
    }
}

public class ModuleDynamicProxy(ModuleDynamicProxyOption option)
    : MoModule<ModuleDynamicProxy, ModuleDynamicProxyOption, ModuleDynamicProxyGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DynamicProxy;
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
