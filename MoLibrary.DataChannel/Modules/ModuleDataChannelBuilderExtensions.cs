using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.DataChannel.Modules;


public static class ModuleDataChannelBuilderExtensions
{
    public static ModuleDataChannelGuide ConfigModuleDataChannel(this WebApplicationBuilder builder,
        Action<ModuleDataChannelOption>? action = null)
    {
        return new ModuleDataChannelGuide().Register(action);
    }
}