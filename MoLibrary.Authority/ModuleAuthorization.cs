using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Authority.Authorization;
using MoLibrary.Authority.Implements.Authorization;
using MoLibrary.Core.Module;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Authority;

public static class ModuleBuilderExtensionsAuthorization
{
     public static ModuleGuideAuthorization AddMoModuleAuthorization<TEnum>(this IServiceCollection services, string claimTypeDefinition) where TEnum : struct, Enum
    {
        return new ModuleGuideAuthorization()
            .AddDefaultPermissionBit<TEnum>(claimTypeDefinition);
    }
}

public class ModuleAuthorization(ModuleOptionAuthorization option) : MoModule<ModuleAuthorization, ModuleOptionAuthorization>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Authority;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {
        services.AddAuthorizationCore();
        services.AddSingleton<IAuthorizationHandler, EnumPermissionRequirementHandler>();
        services.AddTransient<DefaultAuthorizationPolicyProvider>();

        services.AddSingleton<IAuthorizationService, MoAuthorizationService>();
        services.AddSingleton<IMoAuthorizationService, MoAuthorizationService>();
        services.AddSingleton<IMethodInvocationAuthorizationService, MoMethodInvocationAuthorizationService>();

        services.AddTransient<IMoAuthorizationPolicyProvider, MoEnumAuthorizationPolicyProvider>();
        
        var manager = new PermissionBitCheckerManager();
        var checker = new PermissionBitChecker(manager);
        PermissionBitCheckerManager.Singleton = checker;
        services.AddSingleton(_ => manager);
        services.AddSingleton<IPermissionBitChecker, PermissionBitChecker>(_ => checker);
        return Res.Ok();
    }
}

public class ModuleOptionAuthorization : IMoModuleOption<ModuleAuthorization>
{
}

public class ModuleGuideAuthorization : MoModuleGuide<ModuleAuthorization, ModuleOptionAuthorization, ModuleGuideAuthorization>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(AddDefaultPermissionBit)];
    }

    public ModuleGuideAuthorization AddDefaultPermissionBit<TEnum>(string claimTypeDefinition) where TEnum : struct, Enum
    {
        ConfigureExtraServicesOnce(nameof(AddDefaultPermissionBit), services =>
        {
            services.AddMoAuthorizationPermissionBit<TEnum>(claimTypeDefinition);
            services.AddSingleton<IMoPermissionChecker, MoPermissionChecker<TEnum>>();
        });
        return this;
    }
    public ModuleGuideAuthorization AddPermissionBit<TEnum>(string claimTypeDefinition) where TEnum : struct, Enum
    {
        ConfigureExtraServices(nameof(AddPermissionBit), services =>
        {
            var checker = new PermissionBitChecker<TEnum>(claimTypeDefinition);
            PermissionBitCheckerManager.AddChecker(checker);
            services.AddSingleton<IPermissionBitChecker<TEnum>, PermissionBitChecker<TEnum>>(_ => checker);
        });
        return this;
    }
    
    public ModuleGuideAuthorization ConfigAsAlwaysAllow()
    {
        ConfigureExtraServicesOnce(nameof(ConfigAsAlwaysAllow), services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IAuthorizationService, AlwaysAllowAuthorizationService>());
            services.Replace(ServiceDescriptor.Singleton<IMoAuthorizationService, AlwaysAllowAuthorizationService>());
            services.Replace(ServiceDescriptor
                .Singleton<IMethodInvocationAuthorizationService, AlwaysAllowMethodInvocationAuthorizationService>());
            services.Replace(ServiceDescriptor.Singleton<IMoPermissionChecker, AlwaysAllowPermissionChecker>());
        }, EMoModuleOrder.PostConfig);
        return this;
    }
}