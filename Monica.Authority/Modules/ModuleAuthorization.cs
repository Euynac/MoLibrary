using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Authorization.Exceptions;
using Monica.Authority.Authorization.Services.Behaviors;
using Monica.Authority.Authorization.Services;
using Monica.Authority.Authorization.Services.Support;
using Monica.Authority.Localization;
using Monica.Core;
using Monica.Core.Execution;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleAuthorizationBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the Authorization module
        /// </summary>
        public ModuleAuthorizationGuide AddAuthorization<TEnum>(string claimTypeDefinition) where TEnum : struct, Enum
        {
            return builder.AddModule<ModuleAuthorization, ModuleAuthorizationOption, ModuleAuthorizationGuide>()
                .AddDefaultPermissionBit<TEnum>(claimTypeDefinition);
        }
    }
}

[ModuleKey(BuiltInModuleKey.Authority)]
public class ModuleAuthorization(ModuleAuthorizationOption option) : WebModuleBase<ModuleAuthorization, ModuleAuthorizationOption, ModuleAuthorizationGuide>(option)
{
    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        app.UseAuthorization();
    }

    protected override int GetConfigureApplicationBuilderOrder()
    {
        return (int)ModuleApplicationMiddlewareOrder.AfterUseRouting;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<AuthorityMessageLocalizer>();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationHandler, PolicyEnumPermissionRequirementHandler>();
        services.AddTransient<DefaultAuthorizationPolicyProvider>();

        services.AddSingleton<IAuthorizationService, AuthorityAuthorizationService>();
        services.AddSingleton<IAuthorityAuthorizationService, AuthorityAuthorizationService>();
        services.AddSingleton<IExecutionAuthorizationService, ExecutionAuthorizationService>();

        services.AddTransient<IAuthorityAuthorizationPolicyProvider, PolicyEnumAuthorizationProvider>();

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
        DependsOnModule<ModuleExecutionPipelineGuide>().Register()
            .AddBehavior(
                typeof(ExecutionAuthorizationBehavior<,>),
                ExecutionBehaviorOrder.Authorization,
                static descriptor => descriptor.IsBusinessOperation);
    }
}

public class ModuleAuthorizationGuide : WebModuleGuide<ModuleAuthorization, ModuleAuthorizationOption, ModuleAuthorizationGuide>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(AddDefaultPermissionBit)];
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
                .Singleton<IExecutionAuthorizationService, AlwaysAllowExecutionAuthorizationService>());
            context.Services.Replace(ServiceDescriptor.Singleton<IPermissionChecker, AlwaysAllowPermissionChecker>());
        }, ModuleRegistrationOrder.PostConfig);
        return this;
    }

}

public class ModuleAuthorizationOption : ModuleOptions<ModuleAuthorization>
{
    public bool DisableExceptionHandling { get; set; }
}
