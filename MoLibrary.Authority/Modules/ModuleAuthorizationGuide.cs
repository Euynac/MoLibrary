using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
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
        return [nameof(AddDefaultPermissionBit), nameof(AddDefaultMiddleware)];
    }

    internal ModuleAuthorizationGuide AddDefaultMiddleware()
    {
        ConfigureApplicationBuilder(o =>
        {
            o.ApplicationBuilder.UseAuthorization();
        }, EMoModuleApplicationMiddlewaresOrder.AfterUseRouting);
        return this;
    }

    /// <summary>
    /// 专用于判断权限的PermissionBit
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="claimTypeDefinition"></param>
    /// <returns></returns>
    internal ModuleAuthorizationGuide AddDefaultPermissionBit<TEnum>(string claimTypeDefinition) where TEnum : struct, Enum
    {
        ConfigureServices(context =>
        {
            var checker = new PermissionBitChecker<TEnum>(claimTypeDefinition);
            PermissionBitCheckerManager.AddChecker(checker);
            context.Services.AddSingleton<IPermissionBitChecker<TEnum>, PermissionBitChecker<TEnum>>(_ => checker);
            context.Services.AddSingleton<IMoPermissionChecker, MoPermissionChecker<TEnum>>();
        });
        return this;
    }
    /// <summary>
    /// 额外增加新的PermissionBit
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="claimTypeDefinition"></param>
    /// <returns></returns>
    public ModuleAuthorizationGuide AddPermissionBit<TEnum>(string claimTypeDefinition) where TEnum : struct, Enum
    {
        ConfigureServices(context =>
        {
            var checker = new PermissionBitChecker<TEnum>(claimTypeDefinition);
            PermissionBitCheckerManager.AddChecker(checker);
            context.Services.AddSingleton<IPermissionBitChecker<TEnum>, PermissionBitChecker<TEnum>>(_ => checker);
        });
        return this;
    }

    public ModuleAuthorizationGuide ConfigAsAlwaysAllow()
    {
        ConfigureServices(context =>
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
        ConfigureServices(context =>
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