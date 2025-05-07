using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Authority.Authorization;
using MoLibrary.Authority.Implements.Authorization;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.DependencyInjection.DynamicProxy;

namespace MoLibrary.Authority.Modules;

public class ModuleAuthorizationGuide : MoModuleGuide<ModuleAuthorization, ModuleAuthorizationOption, ModuleAuthorizationGuide>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(AddDefaultPermissionBit)];
    }

    public ModuleAuthorizationGuide AddDefaultPermissionBit<TEnum>(string claimTypeDefinition) where TEnum : struct, Enum
    {
        ConfigureExtraServices(nameof(AddDefaultPermissionBit), context =>
        {
            context.Services.AddMoAuthorizationPermissionBit<TEnum>(claimTypeDefinition);
            context.Services.AddSingleton<IMoPermissionChecker, MoPermissionChecker<TEnum>>();
        });
        return this;
    }
    public ModuleAuthorizationGuide AddPermissionBit<TEnum>(string claimTypeDefinition) where TEnum : struct, Enum
    {
        ConfigureExtraServices($"{nameof(AddPermissionBit)}<{typeof(TEnum).Name}>", context =>
        {
            var checker = new PermissionBitChecker<TEnum>(claimTypeDefinition);
            PermissionBitCheckerManager.AddChecker(checker);
            context.Services.AddSingleton<IPermissionBitChecker<TEnum>, PermissionBitChecker<TEnum>>(_ => checker);
        });
        return this;
    }

    public ModuleAuthorizationGuide ConfigAsAlwaysAllow()
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

    public ModuleAuthorizationGuide AddAuthorizationInterceptor()
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