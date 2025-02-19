using BuildingBlocksPlatform.Authority.Authorization;
using BuildingBlocksPlatform.Authority.Implements.Authorization;
using BuildingBlocksPlatform.Authority.Implements.Security;
using BuildingBlocksPlatform.Authority.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocksPlatform.Authority;

public static class MoAuthorizationBuilderExtensions
{
    public static IServiceCollection AddMoAuthorization<TEnum>(this IServiceCollection services, string claimTypeDefinition) where TEnum : struct, Enum
    {
        services.AddAuthorizationCore();
        services.AddSingleton<IAuthorizationHandler, EnumPermissionRequirementHandler>();
        services.AddTransient<DefaultAuthorizationPolicyProvider>();

        services.AddSingleton<IAuthorizationService, MoAuthorizationService>();
        services.AddSingleton<IMoAuthorizationService, MoAuthorizationService>();
        services.AddSingleton<IMethodInvocationAuthorizationService, MoMethodInvocationAuthorizationService>();

        services.AddMoAuthorizationPermissionBit<TEnum>(claimTypeDefinition);
        services.AddSingleton<IMoPermissionChecker, MoPermissionChecker<TEnum>>();

        services.AddTransient<IMoAuthorizationPolicyProvider, MoEnumAuthorizationPolicyProvider>();


        var manager = new PermissionBitCheckerManager();
        var checker = new PermissionBitChecker(manager);
        PermissionBitCheckerManager.Singleton = checker;
        services.AddSingleton(_ => manager);
        services.AddSingleton<IPermissionBitChecker, PermissionBitChecker>(_ => checker);
        return services;
    }
    public static IServiceCollection AddMoAuthorizationPermissionBit<TEnum>(this IServiceCollection services, string claimTypeDefinition) where TEnum : struct, Enum
    {
        var checker = new PermissionBitChecker<TEnum>(claimTypeDefinition);
        PermissionBitCheckerManager.AddChecker(checker);
        services.AddSingleton<IPermissionBitChecker<TEnum>, PermissionBitChecker<TEnum>>(_ => checker);
        return services;
    }
    public static IServiceCollection AddMoAuthorizationAlwaysAllow(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationService, AlwaysAllowAuthorizationService>());
        services.Replace(ServiceDescriptor.Singleton<IMoAuthorizationService, AlwaysAllowAuthorizationService>());
        services.Replace(ServiceDescriptor.Singleton<IMethodInvocationAuthorizationService, AlwaysAllowMethodInvocationAuthorizationService>());
        return services.Replace(ServiceDescriptor.Singleton<IMoPermissionChecker, AlwaysAllowPermissionChecker>());
    }
}
