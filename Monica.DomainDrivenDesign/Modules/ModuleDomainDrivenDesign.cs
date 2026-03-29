using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DomainDrivenDesign.ExceptionHandler;

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
