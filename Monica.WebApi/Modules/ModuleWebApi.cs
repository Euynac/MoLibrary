using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.WebApi.Validation;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

[ModuleKey(BuiltInModuleKey.WebApi)]
public class ModuleWebApi(ModuleWebApiOption option) : ModuleBase<ModuleWebApi, ModuleWebApiOption, ModuleWebApiGuide>(option)
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
        DependsOnModule<ModuleObjectMappingGuide>().Register();
        DependsOnModule<ModuleRepositoryGuide>().Register();
        if (!Option.DisableExceptionHandling)
        {
            DependsOnModule<ModuleExceptionHandlingGuide>().Register()
                .AddExceptionMapper<ValidationExceptionMapper>();
        }
    }
}

public static class ModuleWebApiBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers and configures the Web API infrastructure module.
        /// </summary>
        public static ModuleWebApiGuide AddWebApi(Action<ModuleWebApiOption>? action = null)
        {
            return new ModuleWebApiGuide().Register(action);
        }
    }
}
public class ModuleWebApiGuide : ModuleGuide<ModuleWebApi, ModuleWebApiOption, ModuleWebApiGuide>
{

}

public class ModuleWebApiOption : ModuleOptions<ModuleWebApi>
{
    public bool DisableExceptionHandling { get; set; }
}
