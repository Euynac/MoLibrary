using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.DataChannel.Interfaces;

namespace MoLibrary.DataChannel.Modules;

public class ModuleDataChannelGuide : MoModuleGuide<ModuleDataChannel, ModuleDataChannelOption, ModuleDataChannelGuide>
{

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(SetChannelBuilder)];
    }
    public ModuleDataChannelGuide SetChannelBuilder<TBuilderEntrance>()
    {
        ConfigureServices(nameof(SetChannelBuilder), context =>
        {
            context.Services.AddSingleton(typeof(ISetupPipeline), typeof(TBuilderEntrance));
        });
        return this;
    }
}