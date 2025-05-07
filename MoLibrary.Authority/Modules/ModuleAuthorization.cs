using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Authority.Authorization;
using MoLibrary.Authority.Implements.Authorization;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Authority.Modules;

public class ModuleAuthorization(ModuleAuthorizationOption option) : MoModuleWithDependencies<ModuleAuthorization, ModuleAuthorizationOption>(option)
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