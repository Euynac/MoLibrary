using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Authority.Modules;
using Monica.AutoModel.Exceptions;
using Monica.AutoModel.Modules;
using Monica.Core.ExceptionHandler;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Core.Modules;
using Monica.DependencyInjection.DynamicProxy;
using Monica.DependencyInjection.DynamicProxy.DefaultInterceptors;
using Monica.DependencyInjection.Modules;
using Monica.DomainDrivenDesign.ExceptionHandler;
using Monica.DomainDrivenDesign.Interfaces;
using Monica.Repository.Modules;
using Monica.Tool.Extensions;

namespace Monica.DomainDrivenDesign.Modules;

public class ModuleDomainDrivenDesign(ModuleDomainDrivenDesignOption option) : MoModuleWithDependencies<ModuleDomainDrivenDesign, ModuleDomainDrivenDesignOption, ModuleDomainDrivenDesignGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.DomainDrivenDesign;
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
    extension(Mo)
    {
        /// <summary>
        /// 配置 DomainDrivenDesign 模块
        /// </summary>
        public static ModuleDomainDrivenDesignGuide AddDomainDrivenDesign(Action<ModuleDomainDrivenDesignOption>? action = null)
        {
            return new ModuleDomainDrivenDesignGuide().Register(action);
        }
    }
}
public class ModuleDomainDrivenDesignGuide : MoModuleGuide<ModuleDomainDrivenDesign, ModuleDomainDrivenDesignOption, ModuleDomainDrivenDesignGuide>
{


}

public class ModuleDomainDrivenDesignOption : MoModuleOption<ModuleDomainDrivenDesign>, IMoModuleOptionUseGlobalException
{
    public bool DisableGlobalExceptionHandler { get; set; }
}