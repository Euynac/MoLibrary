using Microsoft.AspNetCore.Builder;

namespace MoLibrary.DataChannel.Modules;


public static class ModuleDataChannelBuilderExtensions
{
    public static ModuleDataChannelGuide ConfigModuleDataChannel(this WebApplicationBuilder builder,
        Action<ModuleDataChannelOption>? action = null)
    {
        return new ModuleDataChannelGuide().Register(action);
    }
}