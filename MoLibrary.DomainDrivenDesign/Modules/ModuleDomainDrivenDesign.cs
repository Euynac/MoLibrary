using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.AutoModel.Modules;
using MoLibrary.Core.Module;
using MoLibrary.DependencyInjection.DynamicProxy.DefaultInterceptors;
using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.DomainDrivenDesign.Interfaces;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;
using MoLibrary.Core.Module.Models;
using MoLibrary.DependencyInjection.Modules;

namespace MoLibrary.DomainDrivenDesign.Modules;

public class ModuleDomainDrivenDesign(ModuleDomainDrivenDesignOption option) : MoModuleWithDependencies<ModuleDomainDrivenDesign, ModuleDomainDrivenDesignOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DomainDrivenDesign;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {


        //TODO 优化无需AOP
        services.AddMoInterceptor<PropertyInjectServiceProviderEmptyInterceptor>().CreateProxyWhenSatisfy(
            c =>
            {
                if (c.ImplementationType.IsAssignableTo<IMoDomainService>() ||
                    c.ImplementationType.IsAssignableTo<IMoApplicationService>())
                {
                    Logger.LogInformation($"service inject:{c.ImplementationType.FullName}");
                    return true;
                }

                return false;
            });
        return Res.Ok();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleAutoModelGuide>().Register();
        DependsOnModule<ModuleDependencyInjectionGuide>().Register();
    }
}