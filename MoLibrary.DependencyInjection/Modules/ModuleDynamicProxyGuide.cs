using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.DependencyInjection.DynamicProxy.Abstract;
using MoLibrary.DependencyInjection.DynamicProxy;

namespace MoLibrary.DependencyInjection.Modules;

public class
    ModuleDynamicProxyGuide : MoModuleGuide<ModuleDynamicProxy, ModuleDynamicProxyOption, ModuleDynamicProxyGuide>
{

    internal ModuleDynamicProxyGuide ConfigDynamicProxyServices()
    {
        PostConfigureServices(nameof(ConfigDynamicProxyServices), context =>
        {
            context.Services.AddSingleton(new ProxyGeneratorWithDI());
            context.Services.AddTransient(typeof(MoAsyncDeterminationInterceptor<>));
            MicrosoftDependencyInjectionDynamicProxyExtensions.ApplyInterceptors(context.Services,
                context.ModuleOption);
        }, EMoModuleOrder.PostConfig);
        return this;
    }
}