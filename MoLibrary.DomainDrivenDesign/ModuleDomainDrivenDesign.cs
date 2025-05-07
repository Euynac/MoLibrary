using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.AutoModel;
using MoLibrary.Core.Module;
using MoLibrary.DependencyInjection;
using MoLibrary.DependencyInjection.DynamicProxy.DefaultInterceptors;
using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.DomainDrivenDesign.Interfaces;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.DomainDrivenDesign;

public static class ModuleBuilderExtensionsAuthorization
{
    public static ModuleGuideDomainDrivenDesign AddMoModuleDomainDrivenDesign(this IServiceCollection services, Action<ModuleOptionDomainDrivenDesign>? action = null)
    {
        return new ModuleGuideDomainDrivenDesign().Register(action);
    }
}

public class ModuleDomainDrivenDesign(ModuleOptionDomainDrivenDesign option) : MoModuleWithDependencies<ModuleDomainDrivenDesign, ModuleOptionDomainDrivenDesign>(option)
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
        DependsOnModule<ModuleGuideAutoModel>().Register();
        DependsOnModule<ModuleGuideDependencyInjection>().Register();
    }
}

public class ModuleGuideDomainDrivenDesign : MoModuleGuide<ModuleDomainDrivenDesign, ModuleOptionDomainDrivenDesign, ModuleGuideDomainDrivenDesign>
{


}
