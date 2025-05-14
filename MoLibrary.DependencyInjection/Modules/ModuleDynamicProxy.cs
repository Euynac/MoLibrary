using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.DependencyInjection.DynamicProxy.Abstract;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DependencyInjection.Modules;

public class ModuleDynamicProxy(ModuleDynamicProxyOption option)
    : MoModule<ModuleDynamicProxy, ModuleDynamicProxyOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DynamicProxy;
    }

    //TODO Order需要最后
    public override Res ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(new ProxyGeneratorWithDI());
        services.AddTransient(typeof(MoAsyncDeterminationInterceptor<>));
        MicrosoftDependencyInjectionDynamicProxyExtensions.ApplyInterceptors(services, option);
        return Res.Ok();
    }
}