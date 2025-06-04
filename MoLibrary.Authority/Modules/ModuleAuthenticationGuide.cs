using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Authority.Security;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Authority.Modules;

public class ModuleAuthenticationGuide : MoModuleGuide<ModuleAuthentication, ModuleAuthenticationOption, ModuleAuthenticationGuide>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(ConfigMoSystemUser)];
    }
    public ModuleAuthenticationGuide ConfigMoSystemUser<T>(T curSystemEnum, Action<MoSystemUserOptions>? action = null) where T : struct, Enum
    {
        ConfigureServices(context =>
        {
            context.Services.Configure((MoSystemUserOptions o) =>
            {
                o.SetCurSystemUser(curSystemEnum);
                action?.Invoke(o);
            });
            context.Services.AddSingleton<IMoSystemUserManager, MoSystemUserManager>();
        });
        return this;
        
    }

    public ModuleAuthenticationGuide ConfigDefaultMoSystemUser(Action<MoSystemUserOptions>? action = null)
    {
        return ConfigMoSystemUser(EMoDefaultSystemUser.System, action);
    }
}