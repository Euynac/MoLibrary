using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Authority.Authorization;
using MoLibrary.Authority.Implements.Authorization;
using MoLibrary.Core.Module;
using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Authority;

public static class ModuleBuilderExtensionsAuthorization
{
    public static ModuleGuideAuthorization AddMoModuleAuthorization<TEnum>(this IServiceCollection services, string claimTypeDefinition) where TEnum : struct, Enum
    {
        return new ModuleGuideAuthorization().Register()
            .AddDefaultPermissionBit<TEnum>(claimTypeDefinition);
    }
}

public class ModuleAuthorization(ModuleOptionAuthorization option) : MoModuleWithDependencies<ModuleAuthorization, ModuleOptionAuthorization>(option)
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

    public override void ClaimDependencies()
    {
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
        ConfigureExtraServices(nameof(AddDefaultPermissionBit), context =>
        {
            context.Services.AddMoAuthorizationPermissionBit<TEnum>(claimTypeDefinition);
            context.Services.AddSingleton<IMoPermissionChecker, MoPermissionChecker<TEnum>>();
        });
        return this;
    }
    public ModuleGuideAuthorization AddPermissionBit<TEnum>(string claimTypeDefinition) where TEnum : struct, Enum
    {
        ConfigureExtraServices($"{nameof(AddPermissionBit)}<{typeof(TEnum).Name}>", context =>
        {
            var checker = new PermissionBitChecker<TEnum>(claimTypeDefinition);
            PermissionBitCheckerManager.AddChecker(checker);
            context.Services.AddSingleton<IPermissionBitChecker<TEnum>, PermissionBitChecker<TEnum>>(_ => checker);
        });
        return this;
    }

    public ModuleGuideAuthorization ConfigAsAlwaysAllow()
    {
        ConfigureExtraServices(nameof(ConfigAsAlwaysAllow), context =>
        {
            context.Services.Replace(ServiceDescriptor.Singleton<IAuthorizationService, AlwaysAllowAuthorizationService>());
            context.Services.Replace(ServiceDescriptor.Singleton<IMoAuthorizationService, AlwaysAllowAuthorizationService>());
            context.Services.Replace(ServiceDescriptor
                .Singleton<IMethodInvocationAuthorizationService, AlwaysAllowMethodInvocationAuthorizationService>());
            context.Services.Replace(ServiceDescriptor.Singleton<IMoPermissionChecker, AlwaysAllowPermissionChecker>());
        }, EMoModuleOrder.PostConfig);
        return this;
    }

    public ModuleGuideAuthorization AddAuthorizationInterceptor()
    {
        ConfigureExtraServices(nameof(AddAuthorizationInterceptor), context =>
        {
            context.Services.AddMoInterceptor<AuthorizationInterceptor>().CreateProxyWhenSatisfy((descriptor) =>
            {
                if (AuthorizationInterceptorRegistrar.ShouldIntercept(descriptor.ImplementationType))
                {
                    //TODO 支持对Controller、OurCRUD进行权限验证
                    //TODO 输出日志
                    //GlobalLog.LogInformation("注入权限验证：{name}", descriptor.ImplementationType.Name);
                    return true;
                }

                return false;
            });
        });
        return this;
    }
}