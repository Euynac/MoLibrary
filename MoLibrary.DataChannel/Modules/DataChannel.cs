using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DataChannel.Modules;


public static class ModuleDataChannelBuilderExtensions
{
    public static ModuleDataChannelGuide AddMoModuleDataChannel(this IServiceCollection services,
        Action<ModuleDataChannelOption>? action = null)
    {
        return new ModuleDataChannelGuide().Register(action);
    }
}

public class ModuleDataChannel(ModuleDataChannelOption option)
    : MoModule<ModuleDataChannel, ModuleDataChannelOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DataChannel;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {

        return Res.Ok();
    }
}

public class ModuleDataChannelGuide : MoModuleGuide<ModuleDataChannel, ModuleDataChannelOption, ModuleDataChannelGuide>
{


}

public class ModuleDataChannelOption : IMoModuleOption<ModuleDataChannel>
{
}