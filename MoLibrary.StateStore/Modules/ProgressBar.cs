using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.StateStore.Modules;


public static class ModuleProgressBarBuilderExtensions
{
    public static ModuleProgressBarGuide ConfigModuleProgressBar(this WebApplicationBuilder builder,
        Action<ModuleProgressBarOption>? action = null)
    {
        return new ModuleProgressBarGuide().Register(action);
    }
}

public class ModuleProgressBar(ModuleProgressBarOption option)
    : MoModule<ModuleProgressBar, ModuleProgressBarOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.ProgressBar;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {

        return Res.Ok();
    }
}

public class ModuleProgressBarGuide : MoModuleGuide<ModuleProgressBar, ModuleProgressBarOption, ModuleProgressBarGuide>
{


}

public class ModuleProgressBarOption : MoModuleOption<ModuleProgressBar>
{
}