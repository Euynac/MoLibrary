using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Authority.Authorization;
using MoLibrary.Authority.Implements.Authorization;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Authority.Modules;

public class ModuleAuthorization(ModuleAuthorizationOption option) : MoModuleWithDependencies<ModuleAuthorization, ModuleAuthorizationOption, ModuleAuthorizationGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Authority;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAuthorization();
        //services.AddAuthorizationCore();
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
    }

    public override void ClaimDependencies()
    {
    }
}