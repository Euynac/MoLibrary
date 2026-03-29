using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DependencyInjection.DynamicProxy.DefaultInterceptors;
using Monica.DomainDrivenDesign.ExceptionHandler;
using Monica.DomainDrivenDesign.Interfaces;
using Monica.Tool.Extensions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

[ModuleKey(EMoModuleKey.DomainDrivenDesign)]
public class ModuleDomainDrivenDesign(ModuleDomainDrivenDesignOption option) : MoModule<ModuleDomainDrivenDesign, ModuleDomainDrivenDesignOption, ModuleDomainDrivenDesignGuide>(option)
{
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleAutoControllersGuide>().Register();
        DependsOnModule<ModuleAutoModelGuide>().Register();
        DependsOnModule<ModuleDependencyInjectionGuide>().Register();
        DependsOnModule<ModuleDynamicProxyGuide>().Register()
            .AddInterceptor<PropertyInjectServiceProviderEmptyInterceptor>(context =>
            {
                if (context.ImplementationType.IsAssignableTo<IMoDomainService>() ||
                    context.ImplementationType.IsAssignableTo<IMoApplicationService>())
                {
                    Logger.LogDebug("Injecting service provider into {ImplementationType}",
                        context.ImplementationType.FullName);
                    return true;
                }

                return false;
            });
        DependsOnModule<ModuleSwaggerGuide>().Register();
        //DependsOnModule<ModuleAuthorizationGuide>().Register().AddDefaultPermissionBit<>();
        DependsOnModule<ModuleAuthenticationGuide>().Register().ConfigDefaultSystemUser();
        DependsOnModule<ModuleMediatorGuide>().Register();
        DependsOnModule<ModuleMapperGuide>().Register();
        DependsOnModule<ModuleRepositoryGuide>().Register();
        if (!Option.DisableExceptionHandling)
        {
            DependsOnModule<ModuleExceptionHandlingGuide>().Register()
                .AddExceptionMapper<ValidationExceptionMapper>();
        }
    }
}

public static class ModuleDomainDrivenDesignBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers and configures the DomainDrivenDesign module. This module also depends on
        /// AutoController.Generator.
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

public class ModuleDomainDrivenDesignOption : MoModuleOption<ModuleDomainDrivenDesign>
{
    public bool DisableExceptionHandling { get; set; }
}
