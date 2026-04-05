using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Authorization.Exceptions;
using Monica.Authority.Authorization.Services;
using Monica.Authority.Authorization.Services.Support;
using Monica.Authority.Localization;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleAuthorizationBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the Authorization module
        /// </summary>
        public static ModuleAuthorizationGuide AddAuthorization<TEnum>(string claimTypeDefinition) where TEnum : struct, Enum
        {
            return new ModuleAuthorizationGuide().Register()
                .AddDefaultPermissionBit<TEnum>(claimTypeDefinition).AddDefaultMiddleware();
        }
    }
}

[ModuleKey(BuiltInModuleKey.Authority)]
public class ModuleAuthorization(ModuleAuthorizationOption option) : ModuleBase<ModuleAuthorization, ModuleAuthorizationOption, ModuleAuthorizationGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<AuthorityMessageLocalizer>();
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
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<AuthorityResource>();

        if (!Option.DisableExceptionHandling)
        {
            DependsOnModule<ModuleExceptionHandlingGuide>().Register()
                .AddExceptionMapper<AuthorizationExceptionMapper>();
        }
        DependsOnModule<ModuleAuthenticationGuide>().Register();
    }
}

public class ModuleAuthorizationGuide : ModuleGuide<ModuleAuthorization, ModuleAuthorizationOption, ModuleAuthorizationGuide>
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
        }, ModuleApplicationMiddlewareOrder.AfterUseRouting);
        return this;
    }

    /// <summary>
    /// Register the PermissionBit definition used for authorization checks
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
    /// Register an additional PermissionBit definition
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
        }, ModuleRegistrationOrder.PostConfig);
        return this;
    }

    public ModuleAuthorizationGuide AddAuthorizationInterceptor()
    {
        DependsOnModule<ModuleDynamicProxyGuide>().Register()
            .AddInterceptor<InterceptionAuthorizer>(descriptor =>
            {
                if (InterceptionRegistrar.ShouldIntercept(descriptor.ImplementationType))
                {
                    //TODO: support permission enforcement on Controller and OurCRUD types
                    //TODO: emit diagnostics for authorization registration
                    //GlobalLog.LogInformation("Injected authorization checks: {name}", descriptor.ImplementationType.Name);
                    return true;
                }

                return false;
            });
        return this;
    }
}

public class ModuleAuthorizationOption : ModuleOptions<ModuleAuthorization>
{
    public bool DisableExceptionHandling { get; set; }
}
