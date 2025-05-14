using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Framework.Modules;


public static class ModuleFrameworkLoggingBuilderExtensions
{
    public static ModuleFrameworkLoggingGuide ConfigModuleFrameworkLogging(this WebApplicationBuilder builder,
        Action<ModuleFrameworkLoggingOption>? action = null)
    {
        return new ModuleFrameworkLoggingGuide().Register(action);
    }
}

public class ModuleFrameworkLogging(ModuleFrameworkLoggingOption option)
    : MoModule<ModuleFrameworkLogging, ModuleFrameworkLoggingOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.FrameworkLogging;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {

        return Res.Ok();
    }
}

public class ModuleFrameworkLoggingGuide : MoModuleGuide<ModuleFrameworkLogging, ModuleFrameworkLoggingOption,
    ModuleFrameworkLoggingGuide>
{


}

public class ModuleFrameworkLoggingOption : IMoModuleOption<ModuleFrameworkLogging>
{
}