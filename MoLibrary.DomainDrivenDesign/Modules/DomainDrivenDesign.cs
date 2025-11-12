using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Authority.Modules;
using MoLibrary.AutoModel.Exceptions;
using MoLibrary.AutoModel.Modules;
using MoLibrary.Core.ExceptionHandler;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.DependencyInjection.DynamicProxy.DefaultInterceptors;
using MoLibrary.DependencyInjection.Modules;
using MoLibrary.DomainDrivenDesign.ExceptionHandler;
using MoLibrary.DomainDrivenDesign.Interfaces;
using MoLibrary.Repository.Modules;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DomainDrivenDesign.Modules;

public class ModuleDomainDrivenDesign(ModuleDomainDrivenDesignOption option) : MoModuleWithDependencies<ModuleDomainDrivenDesign, ModuleDomainDrivenDesignOption, ModuleDomainDrivenDesignGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DomainDrivenDesign;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        //TODO 优化无需AOP
        services.AddMoInterceptor<PropertyInjectServiceProviderEmptyInterceptor>().CreateProxyWhenSatisfy(
            c =>
            {
                if (c.ImplementationType.IsAssignableTo<IMoDomainService>() ||
                    c.ImplementationType.IsAssignableTo<IMoApplicationService>())
                {
                    Logger.LogDebug($"service inject: {c.ImplementationType.FullName}");
                    return true;
                }

                return false;
            });
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleAutoModelGuide>().Register();
        DependsOnModule<ModuleDependencyInjectionGuide>().Register();
        DependsOnModule<ModuleDynamicProxyGuide>().Register();
        DependsOnModule<ModuleGlobalExceptionHandlerGuide>().Register()
            .AddDefaultExceptionHandler();
        DependsOnModule<ModuleSwaggerGuide>().Register();
        DependsOnModule<ModuleGlobalExceptionHandlerGuide>().Register();
        //DependsOnModule<ModuleAuthorizationGuide>().Register().AddDefaultPermissionBit<>();
        DependsOnModule<ModuleAuthenticationGuide>().Register().ConfigDefaultSystemUser();
        DependsOnModule<ModuleMediatorGuide>().Register();
        DependsOnModule<ModuleMapperGuide>().Register();
        DependsOnModule<ModuleRepositoryGuide>().Register();
        if (!Option.DisableGlobalExceptionHandler)
        {
            DependsOnModule<ModuleGlobalExceptionHandlerGuide>().Register()
                .AddMoExceptionHandlerPack<MoValidationExceptionHandler>();
        }
    }
}

public static class ModuleDomainDrivenDesignBuilderExtensions
{
    public static ModuleDomainDrivenDesignGuide ConfigModuleDomainDrivenDesign(this WebApplicationBuilder builder, Action<ModuleDomainDrivenDesignOption>? action = null)
    {
        return new ModuleDomainDrivenDesignGuide().Register(action);
    }
}
public class ModuleDomainDrivenDesignGuide : MoModuleGuide<ModuleDomainDrivenDesign, ModuleDomainDrivenDesignOption, ModuleDomainDrivenDesignGuide>
{


}

public class ModuleDomainDrivenDesignOption : MoModuleOption<ModuleDomainDrivenDesign>, IMoModuleOptionUseGlobalException
{
    public bool DisableGlobalExceptionHandler { get; set; }
}