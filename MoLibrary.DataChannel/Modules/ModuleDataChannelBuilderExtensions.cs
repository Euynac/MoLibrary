using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.DataChannel.Modules;


public static class ModuleDataChannelBuilderExtensions
{
    public static ModuleDataChannelGuide AddMoModuleDataChannel(this IServiceCollection services,
        Action<ModuleDataChannelOption>? action = null)
    {
        return new ModuleDataChannelGuide().Register(action);
    }
}