using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Authority.Authorization;
using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Authorization.Exceptions;
using Monica.Authority.Authorization.Services;
using Monica.Authority.Authorization.Services.Support;
using Monica.Core;
using Monica.Core.ExceptionHandler;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DependencyInjection.DynamicProxy;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleAuthorizationBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Authorization 模块
        /// </summary>
        public static ModuleAuthorizationGuide AddAuthorization<TEnum>(string claimTypeDefinition) where TEnum : struct, Enum
        {
            return new ModuleAuthorizationGuide().Register()
                .AddDefaultPermissionBit<TEnum>(claimTypeDefinition).AddDefaultMiddleware();
        }
    }
}

[ModuleKey(EMoModuleKey.Authority)]
public class ModuleAuthorization(ModuleAuthorizationOption option) : MoModule<ModuleAuthorization, ModuleAuthorizationOption, ModuleAuthorizationGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAuthorization();
        //services.AddAuthorizationCore();
        services.AddSingleton<IAuthorizationHandler, PolicyEnumPermissionRequirementHandler>();
        services.AddTransient<DefaultAuthorizationPolicyProvider>();

        services.AddSingleton<IAuthorizationService, AuthorityAuthorizationService>();
        services.AddSingleton<IAuthorityAuthorizationService, AuthorityAuthorizationService>();
        services.AddSingleton<IMethodInvocationAuthorizationService, MethodInvocationAuthorizationService>();

        services.AddTransient<IAuthorityAuthorizationPolicyProvider, PolicyEnumAuthorizationProvider>();

        var manager = new PermissionBitCheckerManager();
        var checker = new PermissionBitChecker(manager);
        PermissionBitCheckerManager.Singleton = checker;
        services.AddSingleton(_ => manager);
        services.AddSingleton<IPermissionBitChecker, PermissionBitChecker>(_ => checker);
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableGlobalExceptionHandler)
        {
            DependsOnModule<ModuleGlobalExceptionHandlerGuide>().Register().AddMoExceptionHandlerPack<AuthorizationExceptionHandler>();
        }
        DependsOnModule<ModuleAuthenticationGuide>().Register();
    }
}

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
            context.Services.AddSingleton<IPermissionChecker, PermissionChecker<TEnum>>();
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
        }, secondKey: typeof(TEnum).Name);
        return this;
    }

    public ModuleAuthorizationGuide ConfigAsAlwaysAllow()
    {
        ConfigureServices(context =>
        {
            context.Services.Replace(ServiceDescriptor.Singleton<IAuthorizationService, AlwaysAllowAuthorizationService>());
            context.Services.Replace(ServiceDescriptor.Singleton<IAuthorityAuthorizationService, AlwaysAllowAuthorizationService>());
            context.Services.Replace(ServiceDescriptor
                .Singleton<IMethodInvocationAuthorizationService, AlwaysAllowMethodInvocationAuthorizationService>());
            context.Services.Replace(ServiceDescriptor.Singleton<IPermissionChecker, AlwaysAllowPermissionChecker>());
        }, EMoModuleOrder.PostConfig);
        return this;
    }

    public ModuleAuthorizationGuide AddAuthorizationInterceptor()
    {
        ConfigureServices(context =>
        {
            context.Services.AddMoInterceptor<InterceptionAuthorizer>().CreateProxyWhenSatisfy((descriptor) =>
            {
                if (InterceptionRegistrar.ShouldIntercept(descriptor.ImplementationType))
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

public class ModuleAuthorizationOption : MoModuleOption<ModuleAuthorization>, IMoModuleOptionUseGlobalException
{
    public bool DisableGlobalExceptionHandler { get; set; }
}
